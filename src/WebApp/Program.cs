using System.Reflection;
using Farkle;
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

services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Identity")));

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

services.AddSignalR();
services.AddSingleton<Farkle.Application.IGameEventBroadcaster, WebApp.Hubs.SignalRGameEventBroadcaster>();

// Add module services
services.AddFarkleModuleServices(builder.Configuration, logger, new List<Assembly>());

// Broadcast game events from an Eventuous subscription (reacting to persisted events)
// rather than from the endpoints. The subscription registers a hosted service that
// connects to ESDB at startup; the NSwag swagger-generation run has no ESDB, so skip it there.
// Integration-test factories that don't exercise the hub set Farkle:EnableEventBroadcasting=false
// so a live catch-up subscription isn't racing host disposal during their teardown.
if (!builder.Environment.IsEnvironment("NSwag")
    && builder.Configuration.GetValue("Farkle:EnableEventBroadcasting", true))
    services.AddFarkleEventBroadcasting();

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

var requireAuth = builder.Configuration.GetValue<bool>("Auth:RequireAuthorization");
if(requireAuth)
  app.UseAuthentication()
      .UseAuthorization();

app.UseFastEndpoints(c =>
  {
    c.Endpoints.Configurator = ep => { if (requireAuth) ep.Options(b => b.RequireAuthorization()); else ep.Options(b => b.AllowAnonymous());};
  })
   .UseSwaggerGen();

app.MapHub<WebApp.Hubs.GameHub>("/hubs/game");

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly)
    .WithStaticAssets();

if (!app.Environment.IsEnvironment("NSwag"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

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
