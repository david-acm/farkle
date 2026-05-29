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
