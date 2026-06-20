using Microsoft.Extensions.Logging;
using V1 = Farkle.Domain.GameAggregate.GameEvents.V1;
using V2 = Farkle.Domain.GameAggregate.GameEvents.V2;

namespace Farkle.Application;

/// <summary>
/// #33 — emits one structured log event per committed domain event, so every game action
/// (roll, keep, pass, join, win, and rejected commands) lands in Azure Monitor / Application
/// Insights as a queryable custom event. Pure and infra-free: it takes an <see cref="ILogger"/>
/// and the event, so it's trivially unit-testable; the Eventuous wiring lives in
/// <see cref="GameTelemetryHandler"/>.
///
/// Structured properties (EventType, GameId, PlayerId, Position, and the destructured GameEvent)
/// are used instead of string interpolation so they stay queryable in Azure Monitor. GameId and
/// PlayerId are promoted to top-level dimensions (#219) so usage can be sliced by game and by
/// player/seat without parsing the destructured event. Domain events never carry secrets, so
/// attaching the whole event is safe.
/// </summary>
internal static class GameTelemetry
{
  public static void Log(ILogger logger, int gameId, object @event, ulong position) =>
    logger.LogInformation(
      "Game event {EventType} for game {GameId} player {PlayerId} at position {Position} {@GameEvent}",
      @event.GetType().Name, gameId, PlayerIdOf(@event), position, @event);

  // PlayerId is named inconsistently across the events (PlayerId / Id / StartedByPlayerId /
  // TriedToPlay / Player); map each to a single queryable PlayerId dimension. Events with no
  // player (e.g. GameStarted) get null, which Azure Monitor records as an absent dimension.
  private static int? PlayerIdOf(object @event) => @event switch
  {
    V1.PlayerJoined e         => e.Id,
    V2.PlayerJoined e         => e.Id,
    V1.GamePlayStarted e      => e.StartedByPlayerId,
    V1.DiceRolled e           => e.PlayerId,
    V2.DiceRolled e           => e.PlayerId,
    V1.DiceKept e             => e.PlayerId,
    V2.DiceKept e             => e.PlayerId,
    V1.DiceSetAside e         => e.PlayerId,
    V1.DiceReturned e         => e.PlayerId,
    V1.TurnPassed e           => e.PlayerId,
    V1.GameWon e              => e.PlayerId,
    V1.PlayedOutOfTurn e      => e.TriedToPlay,
    V1.RolledTwice e          => e.Player,
    V1.PassedWithoutRolling e => e.PlayerId,
    _                         => null
  };
}
