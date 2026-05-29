using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public interface IGameHubService : IAsyncDisposable
{
    event Action<PassTurnResponse>? OnTurnChanged;
    event Action<LobbyStateResponse>? OnPlayerJoined;
    event Action<LobbyStateResponse>? OnGameBegan;
    Task ConnectAsync(int gameId);
    Task DisconnectAsync();
}
