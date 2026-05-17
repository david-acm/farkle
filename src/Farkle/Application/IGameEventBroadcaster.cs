using static Farkle.Contracts.HttpResponses;

namespace Farkle.Application;

public interface IGameEventBroadcaster
{
    Task BroadcastTurnChangedAsync(PassTurnResponse response, CancellationToken ct);
}
