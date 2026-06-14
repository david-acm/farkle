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
  }

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
