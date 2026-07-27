using Farkle.Client.Lifecycle;
using Farkle.Client.Realtime;

namespace Farkle.Client.Tests.Lifecycle;

/// <summary>
/// The mobile lifecycle rules a desktop browser never exercises: an OS suspends sockets when the
/// app is backgrounded, so the connection must be dropped deliberately and — crucially — the game
/// snapshot re-fetched on resume, because SignalR does not replay what was missed while away.
/// Driven entirely through fakes, so it runs on the desktop with no device or emulator.
/// </summary>
public class GameSessionLifecycleShould
{
    private readonly FakeHub      _hub      = new();
    private readonly FakeSnapshot _snapshot = new();

    public GameSessionLifecycleShould() => _snapshot.Observe(_hub);

    private GameSessionLifecycle CreateLifecycle() => new(_hub, _snapshot);

    [Fact]
    public async Task ConnectWhenTheSessionStarts()
    {
        var lifecycle = CreateLifecycle();

        await lifecycle.StartSessionAsync(42, 7);

        _hub.Connects.Should().ContainSingle().Which.Should().Be((42, 7));
        lifecycle.IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectWhenBackgrounded()
    {
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);

        await lifecycle.OnBackgroundedAsync();

        _hub.Disconnects.Should().Be(1);
        lifecycle.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task ReconnectAndResyncWhenForegrounded()
    {
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);
        await lifecycle.OnBackgroundedAsync();

        await lifecycle.OnForegroundedAsync();

        _hub.Connects.Should().HaveCount(2);
        _snapshot.Refreshes.Should().ContainSingle().Which.Should().Be(42);
    }

    [Fact]
    public async Task ResyncOnlyAfterTheConnectionIsReestablished()
    {
        // Order is load-bearing: refreshing before the socket is live reopens the gap it exists to
        // close — an event broadcast between the fetch and the reconnect would be lost silently.
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);
        await lifecycle.OnBackgroundedAsync();

        await lifecycle.OnForegroundedAsync();

        _snapshot.RefreshedWhileConnected.Should().BeTrue();
    }

    [Fact]
    public async Task IgnoreForegroundingWhenNoSessionIsActive()
    {
        var lifecycle = CreateLifecycle();

        await lifecycle.OnForegroundedAsync();

        _hub.Connects.Should().BeEmpty();
        _snapshot.Refreshes.Should().BeEmpty();
    }

    [Fact]
    public async Task IgnoreBackgroundingWhenNoSessionIsActive()
    {
        var lifecycle = CreateLifecycle();

        await lifecycle.OnBackgroundedAsync();

        _hub.Disconnects.Should().Be(0);
    }

    [Fact]
    public async Task NotConnectTwiceWhenForegroundedWhileAlreadyConnected()
    {
        // iOS/Android both raise their resume event more than once in some transitions
        // (e.g. returning through the app switcher), so this must be idempotent.
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);

        await lifecycle.OnForegroundedAsync();

        _hub.Connects.Should().ContainSingle();
        _snapshot.Refreshes.Should().BeEmpty();
    }

    [Fact]
    public async Task NotDisconnectTwiceWhenBackgroundedRepeatedly()
    {
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);

        await lifecycle.OnBackgroundedAsync();
        await lifecycle.OnBackgroundedAsync();

        _hub.Disconnects.Should().Be(1);
    }

    [Fact]
    public async Task StopRespondingToLifecycleEventsAfterTheSessionEnds()
    {
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);

        await lifecycle.EndSessionAsync();
        await lifecycle.OnForegroundedAsync();

        _hub.Connects.Should().ContainSingle();       // only the original StartSession
        _snapshot.Refreshes.Should().BeEmpty();
        lifecycle.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectWhenTheSessionEnds()
    {
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);

        await lifecycle.EndSessionAsync();

        _hub.Disconnects.Should().Be(1);
    }

    [Fact]
    public async Task SurviveAFailedResumeSoTheNextForegroundCanRetry()
    {
        // A resume often lands before the network is actually back; a throw here would otherwise
        // leave the session wedged in a disconnected state with no path to recover.
        var lifecycle = CreateLifecycle();
        await lifecycle.StartSessionAsync(42, 7);
        await lifecycle.OnBackgroundedAsync();
        _hub.FailNextConnect = true;

        var resume = async () => await lifecycle.OnForegroundedAsync();

        await resume.Should().NotThrowAsync();
        lifecycle.IsConnected.Should().BeFalse();

        await lifecycle.OnForegroundedAsync();
        lifecycle.IsConnected.Should().BeTrue();
        _snapshot.Refreshes.Should().ContainSingle();
    }

    private sealed class FakeHub : IGameHubSession
    {
        public List<(int GameId, int PlayerId)> Connects    { get; } = [];
        public int                              Disconnects { get; private set; }
        public bool                             FailNextConnect { get; set; }
        public bool                             IsConnected { get; private set; }

        public Task ConnectAsync(int gameId, int playerId, CancellationToken ct = default)
        {
            if (FailNextConnect)
            {
                FailNextConnect = false;
                throw new InvalidOperationException("no network");
            }

            Connects.Add((gameId, playerId));
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            Disconnects++;
            IsConnected = false;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSnapshot : IGameSnapshotRefresher
    {
        private IGameHubSession? _hub;

        public List<int> Refreshes                { get; } = [];
        public bool      RefreshedWhileConnected  { get; private set; }

        public void Observe(IGameHubSession hub) => _hub = hub;

        public Task RefreshAsync(int gameId, CancellationToken ct = default)
        {
            Refreshes.Add(gameId);
            RefreshedWhileConnected = _hub?.IsConnected ?? false;
            return Task.CompletedTask;
        }
    }
}
