using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;

namespace Farkle.Application;

/// <summary>
/// #33 — emits one structured log event per committed domain event, so every game action
/// (roll, keep, pass, join, win, and rejected commands) lands in Azure Monitor / Application
/// Insights as a queryable custom event. Pure and infra-free: it takes an <see cref="ILogger"/>
/// and the event, so it's trivially unit-testable; the Eventuous wiring lives in
/// <see cref="GameTelemetryHandler"/>.
///
/// Structured properties (EventType, GameId, Position, and the destructured GameEvent) are used
/// instead of string interpolation so they stay queryable in Azure Monitor. Domain events never
/// carry secrets (no passwords/tokens), so attaching the whole event is safe.
/// </summary>
internal static class GameTelemetry
{
  public static void Log(ILogger logger, int gameId, object @event, ulong position) =>
    logger.LogInformation(
      "Game event {EventType} for game {GameId} at position {Position} {@GameEvent}",
      @event.GetType().Name, gameId, position, @event);
}
