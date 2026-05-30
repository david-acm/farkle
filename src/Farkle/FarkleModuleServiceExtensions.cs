using System.Reflection;
using Eventuous;
using Eventuous.EventStore;
using Farkle.Application;
using Farkle.Contracts;
using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ILogger=Serilog.ILogger;

namespace Farkle;

public static class FarkleModuleServiceExtensions
{
  public static IServiceCollection AddFarkleModuleServices(this IServiceCollection services,
    ConfigurationManager configuration,
    ILogger logger, List<Assembly> mediatrAssemblies)
  {
    mediatrAssemblies.Add(typeof(FarkleModuleServiceExtensions).Assembly);
    
    services.AddCommandService<GameService, Game>();
    services.AddAggregateStore<EsdbEventStore>();
    services.AddSingleton<IGameService, GameService>();
    services.AddSingleton<IGameIdGenerator, RandomGameIdGenerator>();
    services.AddSingleton<IGameCreator, GameCreator>();
    // services.AddRazorComponents();
    
    // TODO: Use Guard clause instead
    // TODO: Use configuration instead. Check the best way to configure the cors url
    var esdbConnString = configuration.GetConnectionString("Esdb") ?? "esdb://localhost:2113?tls=false";
    services.AddEventStoreClient(esdbConnString);
    logger.Information($"Using esdb connection string: {esdbConnString}");
    
    logger.Information("{Module} module services registered", "Farkle.Domain");

    return services;
  }

  public static WebApplication SetUpFarkleModule(this WebApplication app)
  {
    TypeMap.RegisterKnownEventTypes();

    // app.MapCommands(); // Commented out to prevent duplicate routes with FastEndpoints

    return app;
  }
}
