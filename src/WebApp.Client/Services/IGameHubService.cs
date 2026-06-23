using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public interface IGameHubService : IAsyncDisposable
{
    // #221 — the second arg is the originating command's trace id (App Insights operation_Id),
    // propagated from the server broadcast so the client links its UI update back to the command.
    // Null when server-side tracing is off.
    event Action<PassTurnResponse, string?>? OnTurnChanged;
    event Action<LobbyStateResponse, string?>? OnPlayerJoined;
    event Action<LobbyStateResponse, string?>? OnGameBegan;
    event Action<GameStateResponse, string?>? OnTableChanged;
    event Action<GameStateResponse, string?>? OnDiceRolled;
    Task ConnectAsync(int gameId, int playerId);
    Task DisconnectAsync();
}
