using Microsoft.AspNetCore.SignalR;

namespace HotDice.Realtime;

public class GameHub : Hub
{
    public Task JoinGame(int gameId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}");

    public Task LeaveGame(int gameId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"game-{gameId}");
}
