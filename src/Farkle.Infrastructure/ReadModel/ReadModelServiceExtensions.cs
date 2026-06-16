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
    services.AddSingleton<PostgresCheckpointStore>();

    services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
      "GameViewProjector",
      b => b
        .Configure(o => o.EventFilter = StreamFilter.Prefix("Game-"))
        .UseCheckpointStore<PostgresCheckpointStore>()
        .AddEventHandler<GameViewProjector>());

    return services;
  }
}
