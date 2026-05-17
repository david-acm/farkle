using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public interface IGameHubService : IAsyncDisposable
{
    event Action<PassTurnResponse>? OnTurnChanged;
    Task ConnectAsync(int gameId);
    Task DisconnectAsync();
}
