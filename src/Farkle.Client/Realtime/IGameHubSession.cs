namespace Farkle.Client.Realtime;

/// <summary>
/// The connect/disconnect surface of the realtime game session, split out from the full hub service
/// so the lifecycle rules can be unit-tested against a fake with no SignalR server.
/// </summary>
/// <remarks>
/// This is one of the few seams ADR 0010 admits: it exists because it makes off-device testing
/// possible, not to invert a framework dependency (see ADR 0004/0005 — the core embraces its
/// frameworks). <see cref="GameHubService"/> uses <c>HubConnection</c> directly behind it.
/// </remarks>
public interface IGameHubSession
{
    bool IsConnected { get; }

    Task ConnectAsync(int gameId, int playerId, CancellationToken ct = default);

    Task DisconnectAsync(CancellationToken ct = default);
}
