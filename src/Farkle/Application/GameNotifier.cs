using Farkle.Domain.GameAggregate;
using Farkle.Features;
using Marten;

namespace Farkle.Application;

// Post-commit real-time broadcast (ADR 0004). After a command's Wolverine handler has committed
// (IMessageBus.InvokeAsync returned success), the endpoint calls the matching method here; it loads
// the up-to-date GameState snapshot from Marten and pushes the SignalR message. Replaces the
// Eventuous $all GameBroadcastHandler subscription. Because it runs only after a committed
// InvokeAsync, it keeps the "never broadcast a rolled-back write" guarantee; a future refinement
// (#305) can move it onto Wolverine's outbox as a cascading message.
internal sealed class GameNotifier(IQuerySession query, IGameEventBroadcaster broadcaster)
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
