using Eventuous;
using Eventuous.EventStore;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using EventStore.Client;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ILogger = Serilog.ILogger;

namespace Farkle.Infrastructure.Persistence;

/// <summary>
/// Wires the EventStoreDB write side: the gRPC client, the Eventuous ESDB aggregate store, and
/// the real-time broadcast subscription. This is the concrete event-store plug-in that the core
/// (Farkle) deliberately does not reference — the core only depends on Eventuous' abstractions
/// (<c>IAggregateStore</c>, the <c>CommandService</c> / subscription handler base types).
/// </summary>
public static class EventStorePersistenceExtensions
{
  public static IServiceCollection AddFarkleEventStore(
    this IServiceCollection services, IConfiguration configuration, ILogger logger)
  {
    services.AddAggregateStore<EsdbEventStore>();

    var esdbConnString = configuration.GetConnectionString("Esdb") ?? "esdb://localhost:2113?tls=false";
    services.AddEventStoreClient(esdbConnString);
    logger.Information("Using esdb connection string: {EsdbConnectionString}", esdbConnString);

    // Real-time broadcasting: a $all catch-up subscription reacts to persisted Game events and
    // pushes the matching SignalR message (issue #88) instead of firing from the HTTP endpoints.
    // NoOpCheckpointStore: on restart it re-reads from the start, harmless because no SignalR
    // clients are connected then (broadcasts land in empty groups). Gated so hosts without an
    // EventStore (the in-memory storyboard capture) can turn it off.
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

    return services;
  }
}
