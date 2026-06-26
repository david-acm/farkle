using Eventuous;
using Eventuous.EventStore.Subscriptions;
using EventStore.Client;
using Farkle.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Farkle.Infrastructure.ReadModel;

/// <summary>
/// Wires the CQRS read side (#156): the Postgres-backed GameView store and the $all catch-up
/// subscription that keeps it current via <see cref="GameViewProjector"/>. GET reads this view
/// instead of replaying the stream. The host decides whether to call this (it's skipped for
/// hosts without Postgres/ESDB).
/// </summary>
public static class ReadModelServiceExtensions
{
  public static IServiceCollection AddFarkleReadModel(
    this IServiceCollection services, string? connectionString, NpgsqlDataSource? dataSource)
  {
    // Reuses the Identity Postgres database with its own migrations-history table.
    services.AddDbContext<ReadModelDbContext>(o =>
    {
      if (dataSource is not null)
        o.UseNpgsql(dataSource, b => b.MigrationsHistoryTable(ReadModelMigrations.HistoryTable));
      else
        o.UseNpgsql(connectionString, b => b.MigrationsHistoryTable(ReadModelMigrations.HistoryTable));
    });

    // The subscription + GET resolve these from singleton scopes, so the stores open their own
    // DI scope per call (see EfGameViewStore / PostgresCheckpointStore).
    services.AddSingleton<IGameViewStore, EfGameViewStore>();
    services.AddSingleton<IFeedbackViewStore, EfFeedbackViewStore>();
    services.AddSingleton<PostgresCheckpointStore>();

    services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
      "GameViewProjector",
      b => b
        .Configure(o => o.EventFilter = StreamFilter.Prefix("Game-"))
        .UseCheckpointStore<PostgresCheckpointStore>()
        .AddEventHandler<GameViewProjector>());

    // #277 — feedback read model: its own durable $all subscription over the "Feedback-" streams,
    // folding FeedbackSubmitted into FeedbackView for triage. Separate checkpoint id so it's
    // isolated from the game projector.
    services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
      "FeedbackProjector",
      b => b
        .Configure(o => o.EventFilter = StreamFilter.Prefix("Feedback-"))
        .UseCheckpointStore<PostgresCheckpointStore>()
        .AddEventHandler<FeedbackViewProjector>());

    // #33 — a separate, durable subscription that emits structured telemetry per committed event
    // (GameTelemetryHandler). Kept isolated from the projector so a logging hiccup can never
    // affect projection, and durable (its own checkpoint id) so it doesn't re-log the whole
    // stream on restart.
    services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
      "GameTelemetry",
      b => b
        .Configure(o => o.EventFilter = StreamFilter.Prefix("Game-"))
        .UseCheckpointStore<PostgresCheckpointStore>()
        .AddEventHandler<GameTelemetryHandler>());

    return services;
  }
}
