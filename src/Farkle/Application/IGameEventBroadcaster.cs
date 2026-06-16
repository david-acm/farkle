using static Farkle.Contracts.HttpResponses;

namespace Farkle.Application;

public interface IGameEventBroadcaster
{
    Task BroadcastTurnChangedAsync(PassTurnResponse response, CancellationToken ct);

    Task BroadcastPlayerJoinedAsync(LobbyStateResponse lobby, CancellationToken ct);

    Task BroadcastGameBeganAsync(LobbyStateResponse lobby, CancellationToken ct);

    // Pushes the full game-state snapshot to every player in the game so off-turn players
    // see the in-turn player's keeps / set-aside live (#157).
    Task BroadcastTableChangedAsync(GameStateResponse state, CancellationToken ct);

    // Same snapshot, but a distinct message so spectators animate the dice on a roll (#167).
    Task BroadcastDiceRolledAsync(GameStateResponse state, CancellationToken ct);
}
