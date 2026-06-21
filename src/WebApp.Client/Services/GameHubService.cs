using Microsoft.AspNetCore.SignalR.Client;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public sealed class GameHubService(HttpClient http) : IGameHubService
{
    private HubConnection? _connection;
    private int            _gameId;

    public event Action<PassTurnResponse, string?>? OnTurnChanged;
    public event Action<LobbyStateResponse, string?>? OnPlayerJoined;
    public event Action<LobbyStateResponse, string?>? OnGameBegan;
    public event Action<GameStateResponse, string?>? OnTableChanged;
    public event Action<GameStateResponse, string?>? OnDiceRolled;

    public async Task ConnectAsync(int gameId)
    {
        _gameId = gameId;
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

        // After an automatic reconnect the connection has a *new* connection id, so
        // the server-side group membership (game-{gameId}) is lost. Re-join the game
        // group on every reconnect, otherwise the player stops receiving turn/lobby
        // broadcasts until they reload the page.
        _connection.Reconnected += async _ =>
        {
            if (_connection is not null)
                await _connection.InvokeAsync("JoinGame", _gameId);
        };

        await _connection.StartAsync();
        await _connection.InvokeAsync("JoinGame", gameId);
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        try { await _connection.InvokeAsync("LeaveGame", _gameId); } catch { /* best effort */ }
        await _connection.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}
