using Farkle.Client.Realtime;

namespace Farkle.Client.Lifecycle;

/// <summary>
/// Keeps the realtime game session in step with the app's foreground/background lifecycle.
/// </summary>
/// <remarks>
/// A browser tab can hold a socket open indefinitely; a phone cannot — iOS in particular will not
/// keep a WebSocket alive in the background. So the app drops the connection deliberately on
/// background and, on resume, reconnects *and* re-fetches the snapshot, because nothing broadcast
/// during the gap is replayed.
///
/// The shell raises the events (MAUI's <c>Window.Activated</c>/<c>Deactivated</c>); this type holds
/// the rules and depends on nothing platform-specific, so it is covered by desktop unit tests.
/// </remarks>
public sealed class GameSessionLifecycle(IGameHubSession hub, IGameSnapshotRefresher snapshots)
{
    private int? _gameId;
    private int  _playerId;

    /// <summary>True while the realtime connection is live for the active session.</summary>
    public bool IsConnected { get; private set; }

    public async Task StartSessionAsync(int gameId, int playerId, CancellationToken ct = default)
    {
        _gameId   = gameId;
        _playerId = playerId;
        await ConnectAsync(ct);
    }

    public async Task EndSessionAsync(CancellationToken ct = default)
    {
        if (_gameId is null) return;

        await DisconnectAsync(ct);
        _gameId = null;
    }

    public Task OnBackgroundedAsync(CancellationToken ct = default) =>
        _gameId is null || !IsConnected ? Task.CompletedTask : DisconnectAsync(ct);

    public async Task OnForegroundedAsync(CancellationToken ct = default)
    {
        // Resume can fire more than once for a single return to the app (and can fire while the
        // connection was never dropped at all), so reconnecting must be idempotent.
        if (_gameId is null || IsConnected) return;

        if (!await TryConnectAsync(ct)) return;

        await snapshots.RefreshAsync(_gameId.Value, ct);
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        await hub.ConnectAsync(_gameId!.Value, _playerId, ct);
        IsConnected = true;
    }

    private async Task DisconnectAsync(CancellationToken ct)
    {
        await hub.DisconnectAsync(ct);
        IsConnected = false;
    }

    /// <summary>
    /// A resume routinely beats the network back, so a failed reconnect leaves the session intact
    /// and simply stays disconnected — the next foreground event retries. Throwing here would wedge
    /// the session with no path to recover.
    /// </summary>
    private async Task<bool> TryConnectAsync(CancellationToken ct)
    {
        try
        {
            await ConnectAsync(ct);
            return true;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            IsConnected = false;
            return false;
        }
    }
}
