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
using WebApp;
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
  .SwaggerDocument(o => o.DocumentSettings = s => s.DocumentProcessors.Add(new ApiPrefixDocumentProcessor()))
  .AddFastEndpoints(o =>
  {
      o.Assemblies = new[]
      {
          typeof(Farkle.Endpoints.StartGame).Assembly,
          typeof(RegisterEndpoint).Assembly
      };
      o.DisableAutoDiscovery = true;
  });

// Add module services
services.AddFarkleModuleServices(builder.Configuration, logger, new List<Assembly>());

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
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

if (!app.Environment.IsEnvironment("NSwag"))
    app.SetUpFarkleModule();

app.UseAuthentication()
  .UseAuthorization();

app.UseFastEndpoints(c =>
  {
    c.Endpoints.Configurator = ep => ep.Options(b => b.RequireAuthorization());
  })
   .UseSwaggerGen();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Counter).Assembly);

if (!app.Environment.IsEnvironment("NSwag"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();

public partial class Program { }
