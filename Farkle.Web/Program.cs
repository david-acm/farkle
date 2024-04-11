using System.Reflection;
using Farkle;
using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Swagger;
using Serilog;

var logger = Log.Logger = new LoggerConfiguration()
  .Enrich.FromLogContext()
  .WriteTo.Console()
  .CreateLogger();

logger.Information("Starting web host");

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((_, config) =>
  config.ReadFrom.Configuration(builder.Configuration));

await builder.AddAzureAppConfigurationAsync(logger);

var services = builder.Services;
services.AddEndpointsApiExplorer();
services.AddSwaggerGen();
services
  .AddAuthenticationJwtBearer(s =>
  {
    // TODO: Load from config
    s.SigningKey = builder.Configuration["Auth:JwtSecret"];
  })
  .AddAuthorization()
  .SwaggerDocument()
  .AddFastEndpoints();

List<Assembly> mediatRAssemblies = [];
builder.Services.AddFarkleModuleServices(builder.Configuration, logger, mediatRAssemblies);

// await builder.AddAzureAppConfigurationAsync(logger);

// TODO: specify from config
const string myAllowSpecificOrigins = "MyAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
  options.AddPolicy(myAllowSpecificOrigins,
    policy => { policy.WithOrigins("http://localhost:5186").AllowAnyHeader().AllowAnyMethod(); });
});

var app = builder.Build();

// app.UseHttpsRedirection();
app.SetUpFarkleModule();

app.UseAuthentication()
  .UseAuthorization()
  .UseFastEndpoints()
  .UseSwaggerGen();

app.Run();

public partial class Program
{
}

