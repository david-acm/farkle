using Ardalis.SmartEnum.SystemTextJson;
using HotDice.Domain.GameAggregate;
using JasperFx.Events;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Marten;

namespace HotDice;

/// <summary>
/// Registers the Critter Stack write/read path (#302, ADR 0004): Marten as the PostgreSQL event
/// store with <see cref="GameState"/> as the self-aggregating snapshot, integrated with Wolverine as
/// the command bus + transactional outbox. Replaces the Eventuous <c>CommandService</c> + ESDB.
/// </summary>
public static class CritterStackServiceExtensions
{
  /// <summary>
  /// Wires Marten (event store + Inline <see cref="GameState"/> snapshot) integrated with Wolverine
  /// (command bus + transactional outbox) onto the given Postgres connection. This is the single
  /// registration the host calls to stand up the ADR 0004 write/read path.
  /// </summary>
  /// <param name="services">The service collection to register into.</param>
  /// <param name="connectionString">The Postgres connection string Marten/Wolverine use.</param>
  /// <param name="lightweight">
  /// DB-free boot paths (NSwag swagger extraction) set this: Marten never touches the schema and
  /// Wolverine runs mediator-only (no durability agents / message-store tables), so the host boots
  /// without a live Postgres. Marten still connects lazily, so no command actually runs there.
  /// </param>
  /// <returns>The same <paramref name="services"/> instance, for chaining.</returns>
  public static IServiceCollection AddHotDiceCritterStack(
    this IServiceCollection services, string connectionString, bool lightweight = false,
    params System.Reflection.Assembly[] additionalEndpointAssemblies)
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
      // Marten backs Wolverine's inbox/outbox so side effects commit atomically with the event append.
      .IntegrateWithWolverine();

    services.AddWolverine(opts =>
    {
      // Required: without this (and without an [AggregateHandler] on the path) plain handlers that
      // touch IDocumentSession silently don't commit. Turns on the Marten transactional middleware.
      opts.Policies.AutoApplyTransactions();

      // #303 — the Wolverine.HTTP game endpoints live in this (HotDice) assembly's slices.
      opts.Discovery.IncludeAssembly(typeof(CritterStackServiceExtensions).Assembly);
      foreach (var assembly in additionalEndpointAssemblies)
        opts.Discovery.IncludeAssembly(assembly);

      if (lightweight)
        opts.Durability.Mode = DurabilityMode.MediatorOnly;   // no message-store/agents at startup
    });

    return services;
  }
}
