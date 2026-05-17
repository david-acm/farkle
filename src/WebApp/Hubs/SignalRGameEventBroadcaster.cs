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
}
