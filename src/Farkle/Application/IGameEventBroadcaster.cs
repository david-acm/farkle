using static Farkle.Contracts.HttpResponses;

namespace Farkle.Application;

public interface IGameEventBroadcaster
{
    Task BroadcastTurnChangedAsync(PassTurnResponse response, CancellationToken ct);

    Task BroadcastPlayerJoinedAsync(LobbyStateResponse lobby, CancellationToken ct);

    Task BroadcastGameBeganAsync(LobbyStateResponse lobby, CancellationToken ct);
}
