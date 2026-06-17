using Eventuous;
using Eventuous.Subscriptions.Context;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;

namespace Farkle.Application;

/// <summary>
/// #33 — subscribes to the committed Game event stream and emits a structured telemetry event for
/// each domain event (via <see cref="GameTelemetry"/>). This "decorates" the domain events with
/// observability centrally, without touching the pure aggregate or every endpoint: roll, keep,
/// pass, join, win — and rejected commands — all surface as queryable custom events.
///
/// Internal, registered by Farkle.Infrastructure (via InternalsVisibleTo) on its own durable
/// subscription so it never replays the whole stream on restart.
/// </summary>
internal sealed class GameTelemetryHandler : Eventuous.Subscriptions.EventHandler
{
  private readonly ILogger<GameTelemetryHandler> _logger;

  public GameTelemetryHandler(ILogger<GameTelemetryHandler> logger)
  {
    _logger = logger;

    // Lifecycle events.
    Track<GameEvents.V1.GameStarted>();
    Track<GameEvents.V1.PlayerJoined>();
    Track<GameEvents.V2.PlayerJoined>();
    Track<GameEvents.V1.GamePlayStarted>();
    Track<GameEvents.V1.DiceRolled>();
    Track<GameEvents.V2.DiceRolled>();
    Track<GameEvents.V1.DiceKept>();
    Track<GameEvents.V2.DiceKept>();
    Track<GameEvents.V1.DiceSetAside>();
    Track<GameEvents.V1.DiceReturned>();
    Track<GameEvents.V1.TurnPassed>();
    Track<GameEvents.V1.GameWon>();

    // Rejected commands (IErrorEvent) — valuable signals for alerting on misuse / bugs.
    Track<GameEvents.V1.PlayedOutOfTurn>();
    Track<GameEvents.V1.RolledTwice>();
    Track<GameEvents.V1.PassedWithoutRolling>();
  }

  private void Track<T>() where T : class =>
    On<T>(ctx =>
    {
      if (TryGetGameId(ctx.Stream, out var gameId))
        GameTelemetry.Log(_logger, gameId, ctx.Message!, ctx.GlobalPosition);
      return default;
    });

  // Game events are written to the stream "Game-{id}" (StreamNameFactory). Pull the id back out.
  private static bool TryGetGameId(StreamName stream, out int gameId)
  {
    var name = stream.ToString();
    var dash = name.IndexOf('-');
    return int.TryParse(dash >= 0 ? name[(dash + 1)..] : name, out gameId);
  }
}
