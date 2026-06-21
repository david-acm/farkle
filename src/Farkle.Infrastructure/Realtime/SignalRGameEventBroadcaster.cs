using System.Diagnostics;
using Farkle.Application;
using Microsoft.AspNetCore.SignalR;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Infrastructure.Realtime;

public class SignalRGameEventBroadcaster(IHubContext<GameHub> hub) : IGameEventBroadcaster
{
    // #221 — the originating command's trace id (the App Insights operation_Id). At broadcast time
    // Activity.Current is the consumer.GameBroadcasts span, which shares the command's trace (it
    // nests under it, #241), so its TraceId is exactly the id the client links its UI update to.
    // Null when tracing is off (no Activity) — the client then simply omits the correlation prop.
    private static string? OriginatingTraceId() => Activity.Current?.TraceId.ToString();

    public Task BroadcastTurnChangedAsync(PassTurnResponse response, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{response.GameId}")
           .SendAsync("TurnChanged", response, OriginatingTraceId(), ct);

    public Task BroadcastPlayerJoinedAsync(LobbyStateResponse lobby, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{lobby.GameId}")
           .SendAsync("PlayerJoined", lobby, OriginatingTraceId(), ct);

    public Task BroadcastGameBeganAsync(LobbyStateResponse lobby, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{lobby.GameId}")
           .SendAsync("GameBegan", lobby, OriginatingTraceId(), ct);

    public Task BroadcastTableChangedAsync(GameStateResponse state, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{state.GameId}")
           .SendAsync("TableChanged", state, OriginatingTraceId(), ct);

    public Task BroadcastDiceRolledAsync(GameStateResponse state, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{state.GameId}")
           .SendAsync("DiceRolled", state, OriginatingTraceId(), ct);
}
