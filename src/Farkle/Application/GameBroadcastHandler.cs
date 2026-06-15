using Eventuous;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Farkle.Domain.GameAggregate;
using Farkle.Endpoints;
using Microsoft.Extensions.Logging;

namespace Farkle.Application;

/// <summary>
/// Reacts to persisted Game events from an Eventuous subscription and pushes the matching
/// SignalR broadcast. This is the single place real-time updates fire — after the event is
/// committed — replacing the per-endpoint broadcasting (issue #88).
/// </summary>
internal sealed class GameBroadcastHandler : Eventuous.Subscriptions.EventHandler
{
  private readonly IGameService                   _service;
  private readonly IGameEventBroadcaster          _broadcaster;
  private readonly ILogger<GameBroadcastHandler>  _logger;

  public GameBroadcastHandler(
    IGameService                  service,
    IGameEventBroadcaster         broadcaster,
    ILogger<GameBroadcastHandler> logger)
  {
    _service     = service;
    _broadcaster = broadcaster;
    _logger      = logger;

    On<GameEvents.V1.PlayerJoined>(ctx =>
      BroadcastAsync(ctx, (s, ct) => _broadcaster.BroadcastPlayerJoinedAsync(LobbyMapper.ToLobbyState(s), ct)));

    On<GameEvents.V1.GamePlayStarted>(ctx =>
      BroadcastAsync(ctx, (s, ct) => _broadcaster.BroadcastGameBeganAsync(LobbyMapper.ToLobbyState(s), ct)));

    // A winning pass emits TurnPassed then GameWon atomically, so the loaded state already
    // reflects the winner — broadcasting on TurnPassed alone covers the win, no double send.
    On<GameEvents.V1.TurnPassed>(ctx =>
      BroadcastAsync(ctx, (s, ct) =>
        _broadcaster.BroadcastTurnChangedAsync(PassTurnMapper.ToPassTurnResponse(s, ctx.Message.PlayerId), ct)));

    // Rolls and keeps push the full table snapshot to everyone so off-turn players see the
    // in-turn player's dice live (#157). Both V1 and V2 of each event (the app rolls V2).
    // Rolls use a distinct message so spectators animate the dice (#167); keeps don't spin.
    On<GameEvents.V2.DiceRolled>(ctx => BroadcastRolled(ctx));
    On<GameEvents.V1.DiceRolled>(ctx => BroadcastRolled(ctx));
    On<GameEvents.V2.DiceKept>(ctx => BroadcastTable(ctx));
    On<GameEvents.V1.DiceKept>(ctx => BroadcastTable(ctx));

    // Set aside / put back are transient keep selections — broadcasting the table snapshot
    // lets off-turn players watch the in-turn player decide which dice to keep, live (#159).
    On<GameEvents.V1.DiceSetAside>(ctx => BroadcastTable(ctx));
    On<GameEvents.V1.DiceReturned>(ctx => BroadcastTable(ctx));
  }

  private ValueTask BroadcastTable<T>(MessageConsumeContext<T> ctx) where T : class =>
    BroadcastAsync(ctx, (s, ct) => _broadcaster.BroadcastTableChangedAsync(GameStateMapper.ToGameState(s), ct));

  private ValueTask BroadcastRolled<T>(MessageConsumeContext<T> ctx) where T : class =>
    BroadcastAsync(ctx, (s, ct) => _broadcaster.BroadcastDiceRolledAsync(GameStateMapper.ToGameState(s), ct));

  // Seam: load current state by replaying the aggregate, then run the broadcast. A future
  // GameView read model would replace LoadStateAsync here without touching the wiring (#88).
  private async ValueTask BroadcastAsync<T>(
    MessageConsumeContext<T>                  ctx,
    Func<GameState, CancellationToken, Task>  broadcast)
    where T : class
  {
    if (!TryGetGameId(ctx.Stream, out var gameId))
    {
      _logger.LogWarning("Broadcast skipped: could not parse game id from stream {Stream}", ctx.Stream);
      return;
    }

    var state = await _service.LoadStateAsync(new GameId(gameId), ctx.CancellationToken);
    if (state is null) return;

    await broadcast(state, ctx.CancellationToken);
  }

  // Game events are written to the stream "Game-{id}" (StreamNameFactory). Pull the id back out.
  private static bool TryGetGameId(StreamName stream, out int gameId)
  {
    var name = stream.ToString();
    var dash = name.IndexOf('-');
    return int.TryParse(dash >= 0 ? name[(dash + 1)..] : name, out gameId);
  }
}
