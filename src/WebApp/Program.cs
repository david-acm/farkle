using System.Reflection;
using Farkle;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApp.Auth;
using WebApp.Client.Pages;
using WebApp.Components;
using MudBlazor.Services;
using Serilog;
using WebApp.Client;
using Farkle.Infrastructure.Persistence;
using Farkle.Infrastructure.ReadModel;
using Farkle.Infrastructure.Realtime;

var logger = Log.Logger = new LoggerConfiguration()
  .Enrich.FromLogContext()
  .WriteTo.Console()
  .CreateLogger();

logger.Information("Starting web host");

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, config) =>
  config.ReadFrom.Configuration(builder.Configuration));

var services = builder.Services;
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();

// Use Entra (managed-identity) auth only when there is a host but no password — i.e.
// Azure. Local dev + the Testcontainers integration tests supply a password and keep
// plain password auth; when the connection string is unset, defer to EF / the test's
// own override rather than eagerly validating it. See WebApp.IdentityDataSource.
var identityConn = builder.Configuration.GetConnectionString("Identity");
var identityDataSource = !string.IsNullOrEmpty(identityConn)
    && string.IsNullOrEmpty(new Npgsql.NpgsqlConnectionStringBuilder(identityConn).Password)
        ? WebApp.IdentityDataSource.BuildEntra(identityConn)
        : null;
services.AddDbContext<AppDbContext>(o =>
{
    if (identityDataSource is not null)
        o.UseNpgsql(identityDataSource);
    else
        o.UseNpgsql(identityConn);
});

// Readiness checks for the two backing services (tagged "ready"); liveness runs none.
services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("postgres", tags: ["ready"])
    .AddCheck<WebApp.Health.EventStoreHealthCheck>("eventstore", tags: ["ready"]);

services
    .AddIdentityCore<AppUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

services
  .AddAuthenticationJwtBearer(s =>
  {
    s.SigningKey = builder.Configuration["Auth:JwtSecret"];
  })
  .AddAuthorization()
  .SwaggerDocument()
  .AddFastEndpoints(o =>
  {
      o.Assemblies = new[]
      {
          typeof(Farkle.Endpoints.StartGame).Assembly,
          typeof(RegisterEndpoint).Assembly
      };
      o.DisableAutoDiscovery = true;
  });

// Real-time delivery (SignalR + the IGameEventBroadcaster) lives in Farkle.Infrastructure.
services.AddFarkleRealtime();

// CORS: allow only the origins listed in Cors:AllowedOrigins (empty by default, so
// no cross-origin access until configured). Never combine AllowAnyOrigin with
// credentials — that is a CORS misconfiguration browsers reject.
services.AddCors(o =>
  o.AddPolicy("FarklePolicy", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Add module services (domain + application) and the EventStoreDB infrastructure plug-in.
services.AddFarkleModuleServices(builder.Configuration, logger, new List<Assembly>());
services.AddFarkleEventStore(builder.Configuration, logger);

// CQRS read side (#156): a GameView projection in Postgres, kept current by a $all catch-up
// subscription, that GET reads instead of replaying the stream. Disabled for hosts without
// Postgres/ESDB (NSwag spec extraction, the in-memory storyboard capture). It reuses the
// Identity Postgres database (own migrations-history table) — see ReadModelDbContext.
var readModelEnabled = !builder.Environment.IsEnvironment("NSwag")
    && !builder.Configuration.GetValue<bool>("Storyboard:SkipIdentitySeed")
    && builder.Configuration.GetValue("Farkle:ReadModelEnabled", true);
if (readModelEnabled)
{
    services.AddFarkleReadModel(identityConn, identityDataSource);
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["BackendUrl"] ?? "http://localhost:5157")
});
builder.Services.RegisterClientServices();

var app = builder.Build();

// Health endpoints, mapped before CORS/auth/FastEndpoints so they always answer
// anonymously (never gated by Auth:RequireAuthorization). Liveness runs no checks
// (just "process is up"); readiness runs the "ready"-tagged Postgres + EventStore
// checks. These are minimal-API endpoints, so they're absent from the Swagger doc.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // Redirect HTTP → HTTPS in non-development environments (paired with HSTS).
    // Local dev profiles (http/https in launchSettings) are intentionally exempt.
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
// MapStaticAssets requires the static-web-assets endpoint manifest produced by the full
// Blazor WASM build.  NSwag runs the app with noBuild=true to extract the OpenAPI spec,
// so the manifest is absent.  Skipping it in that environment lets NSwag start the host
// successfully without affecting runtime behaviour.
if (!app.Environment.IsEnvironment("NSwag"))
    app.MapStaticAssets();

if (!app.Environment.IsEnvironment("NSwag"))
    app.SetUpFarkleModule();

// Before authentication and the endpoints so preflight + FastEndpoints/Swagger
// responses carry the CORS headers.
app.UseCors("FarklePolicy");

var requireAuth = builder.Configuration.GetValue<bool>("Auth:RequireAuthorization");
if(requireAuth)
  app.UseAuthentication()
      .UseAuthorization();

app.UseFastEndpoints(c =>
  {
    c.Endpoints.Configurator = ep => { if (requireAuth) ep.Options(b => b.RequireAuthorization()); else ep.Options(b => b.AllowAnonymous());};
  })
   .UseSwaggerGen();

app.MapFarkleRealtime();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly)
    .WithStaticAssets();

// Storyboard screenshot tooling boots the host with a stubbed (in-memory) backend and
// no Postgres. Identity isn't exercised there, so the migrate+seed is skipped via this
// flag (default false → normal startup is unchanged).
var skipIdentitySeed = builder.Configuration.GetValue<bool>("Storyboard:SkipIdentitySeed");
if (!app.Environment.IsEnvironment("NSwag") && !skipIdentitySeed)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Apply the read-model schema alongside Identity (same database, separate history table).
    if (readModelEnabled)
        scope.ServiceProvider.GetRequiredService<ReadModelDbContext>().Database.Migrate();

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    const string seedEmail = "player1@email.com";
    if (await userManager.FindByEmailAsync(seedEmail) is null)
    {
        var seedUser = new AppUser { UserName = seedEmail, Email = seedEmail };
        try { await userManager.CreateAsync(seedUser, "Pass@word1"); }
        catch (DbUpdateException) { /* concurrent startup seeded it first — ignore */ }
    }
}

app.Run();

public partial class Program { }
