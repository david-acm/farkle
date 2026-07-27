using System.Reflection;
using System.Text;
using Farkle;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Wolverine.Http;
using Wolverine.Http.FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using WebApp.Auth;
using Farkle.Ui.Pages;
using WebApp.Components;
using MudBlazor.Services;
using Serilog;
using Farkle.Ui;
using WebApp.Telemetry;
using Farkle.Infrastructure;
using Farkle.Infrastructure.Identity;
using Farkle.Realtime;

var logger = Log.Logger = new LoggerConfiguration()
  .Enrich.FromLogContext()
  .WriteTo.Console()
  .CreateLogger();

logger.Information("Starting Farkle WebApp {Version}", WebApp.AppVersion.Current);

var builder = WebApplication.CreateBuilder(args);

// Serilog reads its sinks/levels from configuration (Console everywhere). writeToProviders:true
// forwards log events to the registered ILogger providers too — in particular the OpenTelemetry
// logging provider wired by the Azure Monitor distro (#216) — so logs reach Application Insights
// via OTel, correlated to the active span, while the console stays Serilog. Local dev/tests have
// no connection string, so OTel is a no-op and logging falls back to console.
builder.Host.UseSerilog(
  (context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext(),
  writeToProviders: true);

var services = builder.Services;

// #216 — OpenTelemetry (Azure Monitor distro): traces (requests, dependencies, Marten/Wolverine
// event-store spans with causation across the async handling hop), metrics, and logs to
// Application Insights. Gated on the connection string so local dev/tests don't phone home.
var appInsightsConn = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
services.AddFarkleTelemetry(appInsightsConn);

// #303 — OpenAPI is now sourced from ASP.NET's built-in document generator (Microsoft.AspNetCore.
// OpenApi), which sees the Wolverine.HTTP endpoints. It replaces FastEndpoints.Swagger + NSwag; the
// committed swagger.json is produced by dumping this document (see verify-generated).
services.AddOpenApi();

// Identity persistence (DbContext + stores + the Entra/managed-identity data-source decision)
// lives in Farkle.Infrastructure; it returns the data source so the read model reuses it.
var identityConn = builder.Configuration.GetConnectionString("Identity");
var identityDataSource = services.AddFarkleIdentity(identityConn);

// Readiness checks for the two backing services (tagged "ready"); liveness runs none.
services.AddFarkleHealthChecks();

// #303 — standard ASP.NET JWT bearer (replacing FastEndpoints.Security). HMAC-SHA256 over
// Auth:JwtSecret; issuer/audience are not used, so validate signature + lifetime only. The login
// endpoint mints matching tokens with the IdentityModel handler.
var jwtSecret = builder.Configuration["Auth:JwtSecret"] ?? "farkle-fallback-signing-key-not-used-for-real-tokens";
services
  .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(o =>
  {
    o.TokenValidationParameters = new TokenValidationParameters
    {
      ValidateIssuer           = false,
      ValidateAudience         = false,
      ValidateIssuerSigningKey = true,
      IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
      ValidateLifetime         = true,
    };
  });
services.AddAuthorization();

// Real-time delivery: register SignalR (the hub is mapped below). The broadcast is done directly
// through IHubContext<GameHub> by GameNotifier in the core (ADR 0005).
services.AddSignalR();

// CORS: allow only the origins listed in Cors:AllowedOrigins (empty by default, so
// no cross-origin access until configured). Never combine AllowAnyOrigin with
// credentials — that is a CORS misconfiguration browsers reject.
services.AddCors(o =>
  o.AddPolicy("FarklePolicy", p => p
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
    .AllowAnyHeader()
    .AllowAnyMethod()));

// Add module services (domain + application) and the Critter Stack write/read path: Marten as the
// PostgreSQL event store (GameState is the Inline self-aggregating snapshot GET reads) + Wolverine as
// the command bus (ADR 0004). Marten shares the one Postgres with Identity (its own schema). NSwag
// boots DB-free (lightweight: Marten lazy, Wolverine mediator-only) for swagger extraction.
services.AddFarkleModuleServices(builder.Configuration, logger, new List<Assembly>());
var martenConn = identityConn ?? "Host=localhost;Database=farkle;Username=postgres;Password=postgres";
services.AddFarkleCritterStack(martenConn, lightweight: builder.Environment.IsEnvironment("NSwag"));

// #305 — JasperFx owns the Critter Stack codegen + resource strategy per environment and lights up the
// `dotnet run -- describe | resources | codegen | db-apply | projections` CLI (dispatched at the bottom
// of this file). Marten manages its own schema (CreateOrUpdate — no hand-written event/projection
// migrations); Identity keeps its EF migrations.
//
// Codegen mode: real Production runs on committed, pre-generated code (TypeLoadMode.Static, in
// src/WebApp/Internal/Generated) → fast cold start, no runtime Roslyn, and it fails fast if that code
// is missing. Development (tests + local dev) and the OpenAPI GetDocument boot (the "NSwag" environment,
// which maps to the Production profile but must not depend on committed code being current) generate
// in-memory (Dynamic). verify-codegen keeps the committed code in sync.
var useStaticCodegen = builder.Environment.IsProduction();
services.AddJasperFx(opts =>
{
  // The generated handler/endpoint code is committed under this host project (src/WebApp/Internal/
  // Generated) and compiled into WebApp. AddWolverine is called from the Farkle assembly, so without
  // this JasperFx would look for the pre-built types in Farkle and throw ExpectedTypeMissingException
  // under Static. Point it at the host assembly where the code actually lives.
  opts.ApplicationAssembly = typeof(Program).Assembly;

  opts.Development.GeneratedCodeMode  = TypeLoadMode.Dynamic;
  opts.Development.ResourceAutoCreate  = AutoCreate.CreateOrUpdate;
  opts.Production.GeneratedCodeMode   = useStaticCodegen ? TypeLoadMode.Static : TypeLoadMode.Dynamic;
  opts.Production.ResourceAutoCreate   = AutoCreate.CreateOrUpdate;
  // Fail startup (rather than silently regenerating) if the committed code is missing in prod.
  opts.Production.AssertAllPreGeneratedTypesExist = useStaticCodegen;
});

// #303 — Wolverine.HTTP hosts the game endpoints from their slices (migrating off FastEndpoints).
services.AddWolverineHttp();

// #303 — Wolverine.HTTP defaults the HTTP JSON options to AllowReadingFromString, which makes the
// OpenAPI generator emit integers as a ["integer","string"] union that Kiota can't map to a typed
// property (it falls back to UntypedNode). Force strict number handling so ints stay plain integers
// in the document and the generated client keeps typed int properties.
services.ConfigureHttpJsonOptions(o =>
  o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict);

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

// Health endpoints, mapped before CORS/auth/the Wolverine endpoints so they always answer
// anonymously (never gated by Auth:RequireAuthorization). Liveness runs no checks
// (just "process is up"); readiness runs the "ready"-tagged Postgres check (which
// also covers Marten's event store — it shares the DB). These are minimal-API
// endpoints, so they're absent from the Swagger doc.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

// Anonymous build-version endpoint, mapped here (before CORS/auth) so it always answers —
// lets testers confirm exactly which build they're on. ExcludeFromDescription keeps this ops
// endpoint out of the OpenAPI doc (and the generated Kiota client), like the health checks.
app.MapGet("/version", () => Results.Json(new { version = WebApp.AppVersion.Current }))
   .ExcludeFromDescription();

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

// Before authentication and the endpoints so preflight + Wolverine.HTTP/OpenAPI
// responses carry the CORS headers.
app.UseCors("FarklePolicy");

var requireAuth = builder.Configuration.GetValue<bool>("Auth:RequireAuthorization");
if(requireAuth)
  app.UseAuthentication()
      .UseAuthorization();

// #310 (was #237): Wolverine.HTTP endpoints do NOT run through Wolverine's message-retry policies, so
// a Marten optimistic-concurrency conflict on a concurrent same-stream write (two players / a rapid
// double-tap racing FetchForWriting) otherwise surfaces to the client as a 500. Retry the request
// in-process with a short backoff: on JasperFx.ConcurrencyException re-execute the endpoint, which
// re-fetches the now-advanced stream via [WriteAggregate], re-runs the pure decider, and either
// succeeds or returns a genuine domain error. Safe because the conflict is thrown at commit — before
// any response is written (guarded by !Response.HasStarted). Buffer the body so a retried POST rebinds.
app.Use(async (context, next) =>
{
  // Scope to the game write endpoints (POST /api/games/**) — only they append to a stream and can
  // conflict. SignalR, static assets, auth, and the OpenAPI doc pass straight through, un-buffered.
  var isGameWrite = HttpMethods.IsPost(context.Request.Method)
    && context.Request.Path.StartsWithSegments("/api/games");
  if (!isGameWrite)
  {
    await next(context);
    return;
  }

  TimeSpan[] cooldowns =
    [TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(250)];
  context.Request.EnableBuffering();   // so a retried POST can re-bind its body
  for (var attempt = 0; ; attempt++)
  {
    try
    {
      await next(context);
      return;
    }
    catch (JasperFx.ConcurrencyException) when (attempt < cooldowns.Length && !context.Response.HasStarted)
    {
      await Task.Delay(cooldowns[attempt]);
      context.Request.Body.Position = 0;
    }
  }
});

// #303 — the OpenAPI document (Microsoft.AspNetCore.OpenApi), served at /openapi/v1.json. The
// verify-generated pipeline fetches it to produce the committed swagger.json + Kiota client.
app.MapOpenApi();

// #303 — every game/feedback/auth endpoint is a Wolverine.HTTP method now (FastEndpoints is gone).
// When auth is required, gate the whole group; the anonymous endpoints (login, register, feedback)
// opt out via [AllowAnonymous].
app.MapWolverineEndpoints(opts =>
{
  // #302 follow-up — run the registered FluentValidation validators (AddValidatorsFromAssembly in
  // AddFarkleModuleServices) ahead of any endpoint whose request DTO has one, returning a 400
  // ValidationProblem on failure. Input validity only (out-of-range dice, blank names); business
  // rules remain validation-as-events in the deciders.
  opts.UseFluentValidationProblemDetailMiddleware();

  if (requireAuth)
    opts.RequireAuthorizeOnAll();
});

// #303 — auth endpoints as anonymous minimal APIs (migrated off FastEndpoints). Kept minimal-API
// rather than Wolverine.HTTP because Identity's UserManager isn't resolvable by Wolverine code-gen.
app.MapPost("/api/auth/register", WebApp.Auth.RegisterEndpoint.Post).AllowAnonymous().WithTags("Auth");
app.MapPost("/api/auth/login", WebApp.Auth.LoginEndpoint.Post).AllowAnonymous().WithTags("Auth");

app.MapHub<GameHub>("/hubs/game");

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

    // Concurrent startups can race to apply a pending migration: the E2E harness boots two hosts
    // against one Postgres, and a multi-replica rollout could too. The loser then sees the winner's
    // object already created — a benign duplicate, since the schema ends up correct. Swallow the
    // duplicate-object errors (mirrors the seed-race handling below); rethrow anything else.
    static void MigrateTolerant(DatabaseFacade database)
    {
        try
        {
            database.Migrate();
        }
        catch (PostgresException ex) when (ex.SqlState is "42P07" or "42P06" or "23505")
        {
            // 42P07 duplicate_table / 42P06 duplicate_schema / 23505 unique_violation (history row):
            // another startup applied this migration first — nothing left to do.
        }
    }

    MigrateTolerant(scope.ServiceProvider.GetRequiredService<AppDbContext>().Database);

    // Marten manages its own schema (auto-create); no read-model DbContext migration to apply —
    // the GameView read model is now the Inline GameState snapshot (ADR 0004).

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    const string seedEmail = "player1@email.com";
    if (await userManager.FindByEmailAsync(seedEmail) is null)
    {
        var seedUser = new AppUser { UserName = seedEmail, Email = seedEmail };
        try { await userManager.CreateAsync(seedUser, "Pass@word1"); }
        catch (DbUpdateException) { /* concurrent startup seeded it first — ignore */ }
    }
}

// #305 — dispatch through the JasperFx command line: no args runs the host as before, while
// `dotnet run -- describe | resources | codegen | db-apply | projections` runs the matching tool
// (schema management, codegen, diagnostics) against this exact configuration.
return await app.RunJasperFxCommands(args);

public partial class Program { }
