using JasperFx.Events;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Marten;

namespace Farkle.Features;

/// <summary>
/// Registers the Critter Stack write/read path (#302): Marten as the PostgreSQL event store +
/// Wolverine as the command bus, integrated so Marten backs Wolverine's transactional outbox/inbox.
/// This replaces the Eventuous <c>CommandService</c> + ESDB transport at the cutover.
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
        // (ADR 0002). Set on a clean schema (greenfield migration): switching identity on an
        // existing Guid-keyed schema fails Marten's DDL.
        opts.Events.StreamIdentity = StreamIdentity.AsString;

        // The GameState write-model snapshot and the GameView read projection are registered here
        // as the aggregate is reshaped into Marten conventions (Create/Apply) in the next step.
      })
      // Marten backs Wolverine's inbox/outbox tables so side effects (SignalR broadcast) commit
      // atomically with the event append.
      .IntegrateWithWolverine();

    services.AddWolverine(opts =>
    {
      // Required: without this (and without an [AggregateHandler] on the path) plain handlers that
      // touch IDocumentSession silently don't commit. Turns on the Marten transactional middleware
      // so every handler's session is saved after it runs.
      opts.Policies.AutoApplyTransactions();
    });

    return services;
  }
}
