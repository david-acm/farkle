using Farkle.Client.Realtime;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Ui.Services;

public interface IGameHubService : IGameHubSession, IAsyncDisposable
{
    // #221 — the second arg is the originating command's trace id (App Insights operation_Id),
    // propagated from the server broadcast so the client links its UI update back to the command.
    // Null when server-side tracing is off.
    event Action<PassTurnResponse, string?>? OnTurnChanged;
    event Action<LobbyStateResponse, string?>? OnPlayerJoined;
    event Action<LobbyStateResponse, string?>? OnGameBegan;
    event Action<GameStateResponse, string?>? OnTableChanged;
    event Action<GameStateResponse, string?>? OnDiceRolled;
}
