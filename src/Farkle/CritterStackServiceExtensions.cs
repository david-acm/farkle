using Ardalis.SmartEnum.SystemTextJson;
using Farkle.Domain.GameAggregate;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Marten;

namespace Farkle;

/// <summary>
/// Registers the Critter Stack write/read path (#302, ADR 0004): Marten as the PostgreSQL event
/// store with <see cref="GameState"/> as the self-aggregating snapshot, integrated with Wolverine as
/// the command bus + transactional outbox. Replaces the Eventuous <c>CommandService</c> + ESDB.
/// </summary>
public static class CritterStackServiceExtensions
{
  public static IServiceCollection AddFarkleCritterStack(
    this IServiceCollection services, string connectionString)
  {
    services
      .AddMarten(opts =>
      {
        opts.Connection(connectionString);

        // String stream keys "game-{code}" — the int game code stays the user-facing/API identity
        // (ADR 0002). Set on a clean schema (greenfield migration).
        opts.Events.StreamIdentity = StreamIdentity.AsString;

        // GameState is the self-aggregating snapshot (Create/Apply conventions). Inline for
        // read-your-own-writes (GetGame queries it via IQuerySession).
        opts.Projections.Snapshot<GameState>(SnapshotLifecycle.Inline);

        // Spike-verified: STJ + the SmartEnum value converter is required for GameState to round-trip
        // as a stored document (ImmutableArray<DieValue> otherwise fails to serialize). ImmutableArray<T>
        // and the int-keyed ScoreTable round-trip cleanly under STJ.
        opts.UseSystemTextJsonForSerialization(configure: o =>
          o.Converters.Add(new SmartEnumValueConverter<DieValue, int>()));
      })
      // Marten backs Wolverine's inbox/outbox so side effects (SignalR broadcast) commit atomically
      // with the event append.
      .IntegrateWithWolverine();

    services.AddWolverine(opts =>
    {
      // Required: without this (and without an [AggregateHandler] on the path) plain handlers that
      // touch IDocumentSession silently don't commit. Turns on the Marten transactional middleware.
      opts.Policies.AutoApplyTransactions();
    });

    return services;
  }
}
