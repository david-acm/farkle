using System.Reflection;
using Eventuous;
using Eventuous.EventStore;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Farkle.Application;
using Farkle.Contracts;
using Farkle.Domain.GameAggregate;
using Farkle.Subscriptions;
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

  public static IServiceCollection AddFarkleEventBroadcasting(this IServiceCollection services)
  {
    // Scope the broadcast subscription to the Game category ($ce-Game) rather than the global
    // $all stream, so the reader only ingests Game-* aggregate events. ResolveLinkTos resolves
    // each category link back to the original domain event, so ctx.Stream is the underlying
    // Game-{id} stream (used by the handler to load aggregate state) and ctx.Message is the
    // domain event the On<T> handlers match on.
    services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
      "GameBroadcasts",
      builder => builder
        .Configure(options =>
        {
          options.StreamName     = new StreamName("$ce-Game");
          options.ResolveLinkTos = true;
        })
        .UseCheckpointStore<NoOpCheckpointStore>()
        .AddEventHandler<GameBroadcastHandler>());
    return services;
  }

  public static WebApplication SetUpFarkleModule(this WebApplication app)
  {
    TypeMap.RegisterKnownEventTypes();

    // app.MapCommands(); // Commented out to prevent duplicate routes with FastEndpoints

    return app;
  }
}
