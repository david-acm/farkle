using Farkle.Domain.GameAggregate;
using Farkle.Features;
using Marten;

namespace Farkle.Application;

// Post-commit real-time broadcast (ADR 0004). Loads the up-to-date GameState snapshot from Marten and
// pushes the SignalR message. Since #303 it is driven by GameBroadcastHandler, which consumes the
// GameNotifications cascaded by the Wolverine.HTTP endpoints via the Marten outbox — so it runs only
// after the event append commits ("never broadcast a rolled-back write").
public sealed class GameNotifier(IQuerySession query, IGameEventBroadcaster broadcaster)
{
  public Task LobbyChangedAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => broadcaster.BroadcastPlayerJoinedAsync(LobbyMapper.ToLobbyState(s), ct));

  public Task GameBeganAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => broadcaster.BroadcastGameBeganAsync(LobbyMapper.ToLobbyState(s), ct));

  public Task DiceRolledAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => broadcaster.BroadcastDiceRolledAsync(GameStateMapper.ToGameState(s), ct));

  public Task TableChangedAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => broadcaster.BroadcastTableChangedAsync(GameStateMapper.ToGameState(s), ct));

  public Task TurnChangedAsync(int gameId, int playerId, CancellationToken ct) =>
    WithState(gameId, ct, s => broadcaster.BroadcastTurnChangedAsync(PassTurnMapper.ToPassTurnResponse(s, playerId), ct));

  private async Task WithState(int gameId, CancellationToken ct, Func<GameState, Task> broadcast)
  {
    var state = await query.LoadAsync<GameState>($"game-{gameId}", ct);
    if (state is not null) await broadcast(state);
  }
}
