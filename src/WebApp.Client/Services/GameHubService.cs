using Farkle.Client.Realtime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public sealed class GameHubService(HttpClient http, IUiTelemetry telemetry) : IGameHubService
{
    private HubConnection? _connection;
    private int            _gameId;
    private int            _playerId;

    // #242 — real-time connection health. Emit Realtime.* events so reconnect storms / drops are
    // queryable, keyed on playerId (the SignalR connectionId is ephemeral — new on every reconnect).
    private Task TrackConnectionAsync(string @event, string? detail = null) =>
        telemetry.TrackIntentAsync(@event, new Dictionary<string, string?>
        {
            ["GameId"]   = _gameId.ToString(),
            ["PlayerId"] = _playerId.ToString(),
            ["detail"]   = detail,
        });

    public event Action<PassTurnResponse, string?>? OnTurnChanged;
    public event Action<LobbyStateResponse, string?>? OnPlayerJoined;
    public event Action<LobbyStateResponse, string?>? OnGameBegan;
    public event Action<GameStateResponse, string?>? OnTableChanged;
    public event Action<GameStateResponse, string?>? OnDiceRolled;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public async Task ConnectAsync(int gameId, int playerId, CancellationToken ct = default)
    {
        _gameId = gameId;
        _playerId = playerId;
        var hubUrl = $"{http.BaseAddress!.ToString().TrimEnd('/')}/hubs/game";
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        // #221 — each broadcast carries the originating command's trace id as a second arg.
        _connection.On<PassTurnResponse, string?>("TurnChanged",
            (payload, traceId) => OnTurnChanged?.Invoke(payload, traceId));
        _connection.On<LobbyStateResponse, string?>("PlayerJoined",
            (payload, traceId) => OnPlayerJoined?.Invoke(payload, traceId));
        _connection.On<LobbyStateResponse, string?>("GameBegan",
            (payload, traceId) => OnGameBegan?.Invoke(payload, traceId));
        _connection.On<GameStateResponse, string?>("TableChanged",
            (payload, traceId) => OnTableChanged?.Invoke(payload, traceId));
        _connection.On<GameStateResponse, string?>("DiceRolled",
            (payload, traceId) => OnDiceRolled?.Invoke(payload, traceId));

        // #242 — connection dropped, auto-reconnect retrying (flaky network / server restart).
        _connection.Reconnecting += error => TrackConnectionAsync("Realtime.Reconnecting", error?.GetType().Name);

        // After an automatic reconnect the connection has a *new* connection id, so
        // the server-side group membership (game-{gameId}) is lost. Re-join the game
        // group on every reconnect, otherwise the player stops receiving turn/lobby
        // broadcasts until they reload the page.
        _connection.Reconnected += async _ =>
        {
            await TrackConnectionAsync("Realtime.Reconnected"); // #242
            if (_connection is not null)
                await _connection.InvokeAsync("JoinGame", _gameId);
        };

        // #242 — connection fully closed. error == null is a clean stop (LeaveGame/navigate);
        // non-null is an unexpected drop that auto-reconnect couldn't recover (feeds abandonment).
        _connection.Closed += error => TrackConnectionAsync("Realtime.Disconnected", error is null ? "clean" : error.GetType().Name);

        await _connection.StartAsync(ct);
        await _connection.InvokeAsync("JoinGame", gameId, ct);
        await TrackConnectionAsync("Realtime.Connected"); // #242
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        if (_connection is null) return;
        // Best effort: we are tearing the connection down regardless, so a failure to say goodbye
        // is not worth surfacing. Narrowed to what InvokeAsync can actually raise on a dying
        // connection — a blanket catch would also swallow real defects.
        try
        {
            await _connection.InvokeAsync("LeaveGame", _gameId, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HubException or IOException
                                      or ObjectDisposedException or OperationCanceledException)
        {
            // Not silent: the teardown continues regardless, but the failed goodbye is recorded on
            // the same Realtime.* channel as every other connection event (#242), so a game whose
            // players never left the server group is visible rather than guesswork.
            await TrackConnectionAsync("Realtime.LeaveFailed", ex.GetType().Name);
        }
        await _connection.StopAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
