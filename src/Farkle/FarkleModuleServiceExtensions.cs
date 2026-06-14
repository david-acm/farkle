using System.Reflection;
using Eventuous;
using Eventuous.EventStore;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using EventStore.Client;
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

    // Real-time broadcasting: a $all catch-up subscription reacts to persisted Game
    // events and pushes the matching SignalR message (issue #88) — instead of firing
    // the broadcast from inside the HTTP endpoints. NoOpCheckpointStore: on restart it
    // re-reads from the start, which is harmless because no SignalR clients are
    // connected then (broadcasts land in empty groups).
    //
    // Gated so hosts without an EventStore (the in-memory storyboard capture) can turn
    // it off — the subscription needs the EventStoreClient to run.
    if (configuration.GetValue("Farkle:RealtimeBroadcastEnabled", true))
    {
      services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
        "GameBroadcasts",
        builder => builder
          // Server-side filter $all down to the Game aggregate streams ("Game-{id}"), so
          // ESDB system events and other categories never reach the handler.
          .Configure(o => o.EventFilter = StreamFilter.Prefix("Game-"))
          .UseCheckpointStore<NoOpCheckpointStore>()
          .AddEventHandler<GameBroadcastHandler>());
    }

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
