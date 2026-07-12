using System.Diagnostics;
using Farkle.Domain.GameAggregate;
using Farkle.Features;
using Farkle.Realtime;
using Marten;
using Microsoft.AspNetCore.SignalR;

namespace Farkle.Application;

// Post-commit real-time broadcast (ADR 0004/0005). Loads the up-to-date GameState snapshot from Marten
// and pushes the SignalR message straight through IHubContext — no IGameEventBroadcaster port. Since
// #303 it is driven by GameBroadcastHandler, which consumes the GameNotifications cascaded by the
// Wolverine.HTTP endpoints via the Marten outbox — so it runs only after the event append commits
// ("never broadcast a rolled-back write"). ADR 0005: the core embraces SignalR directly, the same way
// it already embraces Marten/Wolverine — the port was ceremony with a single implementation.
public sealed class GameNotifier(IQuerySession query, IHubContext<GameHub> hub)
{
  public Task LobbyChangedAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => Send(gameId, "PlayerJoined", LobbyMapper.ToLobbyState(s), ct));

  public Task GameBeganAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => Send(gameId, "GameBegan", LobbyMapper.ToLobbyState(s), ct));

  public Task DiceRolledAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => Send(gameId, "DiceRolled", GameStateMapper.ToGameState(s), ct));

  public Task TableChangedAsync(int gameId, CancellationToken ct) =>
    WithState(gameId, ct, s => Send(gameId, "TableChanged", GameStateMapper.ToGameState(s), ct));

  public Task TurnChangedAsync(int gameId, int playerId, CancellationToken ct) =>
    WithState(gameId, ct, s => Send(gameId, "TurnChanged", PassTurnMapper.ToPassTurnResponse(s, playerId), ct));

  private Task Send(int gameId, string message, object payload, CancellationToken ct) =>
    hub.Clients.Group($"game-{gameId}").SendAsync(message, payload, OriginatingTraceId(), ct);

  // #221 — the originating command's trace id (the App Insights operation_Id). At broadcast time
  // Activity.Current is the consumer.GameBroadcasts span, which shares the command's trace (#241), so
  // its TraceId is exactly the id the client links its UI update to. Null when tracing is off.
  private static string? OriginatingTraceId() => Activity.Current?.TraceId.ToString();

  private async Task WithState(int gameId, CancellationToken ct, Func<GameState, Task> broadcast)
  {
    var state = await query.LoadAsync<GameState>($"game-{gameId}", ct);
    if (state is not null) await broadcast(state);
  }
}
