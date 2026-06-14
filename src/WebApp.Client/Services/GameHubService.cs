using Microsoft.AspNetCore.SignalR.Client;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public sealed class GameHubService(HttpClient http) : IGameHubService
{
    private HubConnection? _connection;
    private int            _gameId;

    public event Action<PassTurnResponse>? OnTurnChanged;
    public event Action<LobbyStateResponse>? OnPlayerJoined;
    public event Action<LobbyStateResponse>? OnGameBegan;
    public event Action<GameStateResponse>? OnTableChanged;

    public async Task ConnectAsync(int gameId)
    {
        _gameId = gameId;
        var hubUrl = $"{http.BaseAddress!.ToString().TrimEnd('/')}/hubs/game";
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<PassTurnResponse>("TurnChanged",
            payload => OnTurnChanged?.Invoke(payload));
        _connection.On<LobbyStateResponse>("PlayerJoined",
            payload => OnPlayerJoined?.Invoke(payload));
        _connection.On<LobbyStateResponse>("GameBegan",
            payload => OnGameBegan?.Invoke(payload));
        _connection.On<GameStateResponse>("TableChanged",
            payload => OnTableChanged?.Invoke(payload));

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
