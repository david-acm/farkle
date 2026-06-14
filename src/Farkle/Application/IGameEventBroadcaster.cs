using static Farkle.Contracts.HttpResponses;

namespace Farkle.Application;

public interface IGameEventBroadcaster
{
    Task BroadcastTurnChangedAsync(PassTurnResponse response, CancellationToken ct);

    Task BroadcastPlayerJoinedAsync(LobbyStateResponse lobby, CancellationToken ct);

    Task BroadcastGameBeganAsync(LobbyStateResponse lobby, CancellationToken ct);

    // Pushes the full game-state snapshot to every player in the game so off-turn players
    // see the in-turn player's rolls and keeps live (#157).
    Task BroadcastTableChangedAsync(GameStateResponse state, CancellationToken ct);
}
