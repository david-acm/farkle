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

// await builder.AddAzureAppConfigurationAsync(logger);

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
    policy => { policy.WithOrigins("http://localhost:8000").AllowAnyHeader().AllowAnyMethod(); });
});

var app = builder.Build();

// app.UseHttpsRedirection();
app.SetUpFarkleModule();

app.UseAuthentication()
  .UseAuthorization()
  .UseFastEndpoints()
  .UseRouting()
  .UseSwaggerGen();
//
// app.UseBlazorFrameworkFiles();
// app.UseStaticFiles();
// app.MapFallbackToFile("index.html");
// app.UseRouting();
// app.UseAuthorization();
// app.UseFastEndpoints(c =>
// {
//   c.Endpoints.ShortNames                    = true;
//   c.Serializer.Options.PropertyNamingPolicy = null;
// });

app.Run();

public partial class Program
{
}

