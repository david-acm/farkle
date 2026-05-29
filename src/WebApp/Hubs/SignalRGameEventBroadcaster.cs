using Farkle.Application;
using Microsoft.AspNetCore.SignalR;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Hubs;

public class SignalRGameEventBroadcaster(IHubContext<GameHub> hub) : IGameEventBroadcaster
{
    public Task BroadcastTurnChangedAsync(PassTurnResponse response, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{response.GameId}")
           .SendAsync("TurnChanged", response, ct);

    public Task BroadcastPlayerJoinedAsync(LobbyStateResponse lobby, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{lobby.GameId}")
           .SendAsync("PlayerJoined", lobby, ct);

    public Task BroadcastGameBeganAsync(LobbyStateResponse lobby, CancellationToken ct) =>
        hub.Clients
           .Group($"game-{lobby.GameId}")
           .SendAsync("GameBegan", lobby, ct);
}
