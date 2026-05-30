using System.Net.Http.Headers;
using System.Text;
using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.WebTests;

public class GameHubShould : IClassFixture<GameApiWebAppFactory>
{
    private readonly GameApiWebAppFactory _factory;
    private readonly HttpClient           _httpClient;
    private readonly FarkleApiClient      _client;

    public GameHubShould(GameApiWebAppFactory factory)
    {
        _factory = factory;
        var inner   = factory.Server.CreateHandler();
        var wrapped = new HttpClient(new EmptyBodyJsonHandler(inner))
        {
            BaseAddress = factory.Server.BaseAddress
        };
        _httpClient = wrapped;
        var adapter = new HttpClientRequestAdapter(
            new AnonymousAuthenticationProvider(), httpClient: _httpClient);
        adapter.BaseUrl = _httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
        _client = new FarkleApiClient(adapter);

        AuthenticateAsync().GetAwaiter().GetResult();
    }

    private async Task AuthenticateAsync()
    {
        var email    = $"hub-test-{Guid.NewGuid():N}@farkle.dev";
        const string password = "Test@123!";

        await _client.Api.Auth.Register.PostAsync(
            new WebAppAuthRegisterRequest { Email = email, Password = password });

        var login = await _client.Api.Auth.Login.PostAsync(
            new WebAppAuthLoginRequest { Email = email, Password = password });

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    [Fact]
    public async Task BroadcastsTurnChangedAfterPassTurn()
    {
        // The server generates the id, so create the game first, then subscribe the hub.
        var gameId = (await _client.Api.Games.PostAsync())!.Id!.Value;

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game",
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        PassTurnResponse? received = null;
        var tcs = new TaskCompletionSource<PassTurnResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<PassTurnResponse>("TurnChanged", payload =>
        {
            received = payload;
            tcs.TrySetResult(payload);
        });

        await connection.StartAsync();
        await connection.InvokeAsync("JoinGame", gameId);

        var player1 = (await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "David" }))?.Id ?? 0;

        await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "Allison" });

        await _client.Api.Games[gameId].Start.PostAsync(
            new FarkleContractsHttpRequests_BeginGameRequest { PlayerId = 1 });

        var roll = await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();
        var scoringDice = (roll!.DiceValues ?? []).Where(v => v == 1 || v == 5).Select(v => (int)v!).ToArray();
        if (scoringDice.Length > 0)
            await _client.Api.Games[gameId].Players[player1].Keeps.PostAsync(
                new FarkleContractsHttpRequests_KeepDiceRequest
                {
                    DiceValues = scoringDice.Select(v => (int?)v).ToList()
                });

        await _client.Api.Games[gameId].Players[player1].Turns.PostAsync();

        // Wait up to 15 s for the hub message (broadcast now flows through a catch-up subscription).
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast TurnChanged within 15 seconds");

        Assert.NotNull(received);
        Assert.Equal(2, received!.CurrentPlayerId); // turn rotated to player 2
        Assert.Equal(gameId, received.GameId);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task BroadcastsPlayerJoinedWhenAPlayerJoins()
    {
        var gameId = (await _client.Api.Games.PostAsync())!.Id!.Value;

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game",
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var tcs = new TaskCompletionSource<LobbyStateResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<LobbyStateResponse>("PlayerJoined", payload => tcs.TrySetResult(payload));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinGame", gameId);

        await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "David" });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast PlayerJoined within 15 seconds");

        var lobby = await tcs.Task;
        Assert.Equal(gameId, lobby.GameId);
        Assert.Equal("WaitingForPlayers", lobby.Stage);
        Assert.Contains(lobby.Roster, p => p.Name == "David");

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task BroadcastsGameBeganWhenHostStarts()
    {
        var gameId = (await _client.Api.Games.PostAsync())!.Id!.Value;

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game",
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var tcs = new TaskCompletionSource<LobbyStateResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<LobbyStateResponse>("GameBegan", payload => tcs.TrySetResult(payload));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinGame", gameId);

        await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "David" });
        await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "Allison" });
        await _client.Api.Games[gameId].Start.PostAsync(
            new FarkleContractsHttpRequests_BeginGameRequest { PlayerId = 1 });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast GameBegan within 15 seconds");

        var lobby = await tcs.Task;
        Assert.Equal(gameId, lobby.GameId);
        Assert.Equal("Rolling", lobby.Stage);

        await connection.DisposeAsync();
    }
}

file sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method == HttpMethod.Post && request.Content == null)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return base.SendAsync(request, ct);
    }
}
