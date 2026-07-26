namespace Farkle.Client.Lifecycle;

/// <summary>
/// Re-fetches the authoritative game state after a gap in the realtime stream.
/// </summary>
/// <remarks>
/// SignalR does not replay what was broadcast while the socket was down, so a resumed app would
/// otherwise render state frozen at the moment it was backgrounded. The client owns the fetch (it
/// knows where the state lives — BlazorState on the web); this seam only says *when*.
/// </remarks>
public interface IGameSnapshotRefresher
{
    Task RefreshAsync(int gameId, CancellationToken ct = default);
}
