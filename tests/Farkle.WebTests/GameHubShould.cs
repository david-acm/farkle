using System.Net.Http.Headers;
using System.Text;
using Farkle.ApiClient;
using Farkle.ApiClient.Models;
using Farkle.SharedKernel.Scoring;
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

        // Wait up to 5 s for the hub message.
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast TurnChanged within 5 seconds");

        Assert.NotNull(received);
        Assert.Equal(2, received!.CurrentPlayerId); // turn rotated to player 2
        Assert.Equal(gameId, received.GameId);

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task BroadcastsDiceRolledWhenTheInTurnPlayerRolls()
    {
        // Any player in the game group (here, a spectator-style connection) should receive the
        // in-turn player's roll live via the DiceRolled snapshot — a distinct message from the
        // generic TableChanged so spectators animate the dice on a roll (#157, #167).
        var gameId = (await _client.Api.Games.PostAsync())!.Id!.Value;

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game",
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        var tcs = new TaskCompletionSource<GameStateResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<GameStateResponse>("DiceRolled", payload => tcs.TrySetResult(payload));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinGame", gameId);

        var player1 = (await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "David" }))?.Id ?? 0;
        await _client.Api.Games[gameId].Players.PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "Allison" });
        await _client.Api.Games[gameId].Start.PostAsync(
            new FarkleContractsHttpRequests_BeginGameRequest { PlayerId = 1 });

        await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast DiceRolled within 5 seconds");

        var table = await tcs.Task;
        Assert.Equal(gameId, table.GameId);
        Assert.Equal(6, table.TableCenter.Count); // a fresh roll puts all six dice in the centre

        await connection.DisposeAsync();
    }

    [Fact]
    public async Task BroadcastsTableChangedWhenTheInTurnPlayerSetsADieAside()
    {
        // #159 — set aside is a first-class command/event, so spectators see the in-turn
        // player's keep selection live via the TableChanged snapshot.
        //
        // The roll uses real randomness. A greedy MachinePlayer keeps the best-scoring trick
        // of the roll (via the shared ScoreCalculator); the setup retries only on a true
        // Farkle (nothing scores at all), so the test sets aside a realistic, best-possible
        // selection rather than a single hand-picked 1/5.
        var (gameId, player1, bestKeep) = await StartGameAndRollBestKeepAsync();

        var connection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/game",
                o => o.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler())
            .Build();

        await connection.StartAsync();
        await connection.InvokeAsync("JoinGame", gameId);

        // Capture the broadcast once the whole best-trick selection has been set aside.
        var tcs = new TaskCompletionSource<GameStateResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<GameStateResponse>("TableChanged", payload =>
        {
            if (payload.DiceSetAside.Count >= bestKeep.Count) tcs.TrySetResult(payload);
        });

        // Set aside the best trick — one die per command (SetDiceAside takes a single die).
        foreach (var die in bestKeep)
            await _client.Api.Games[gameId].Players[player1].Setasides.PostAsync(
                new FarkleContractsHttpRequests_SetDiceAsideRequest { DieValue = die });

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast the set-aside TableChanged within 5 seconds");

        var table = await tcs.Task;
        Assert.Equal(gameId, table.GameId);
        // The broadcast carries the full best-trick selection (compare as a multiset).
        Assert.Equal(bestKeep.OrderBy(d => d), table.DiceSetAside.OrderBy(d => d));

        await connection.DisposeAsync();
    }

    // Starts a two-player game and rolls once, retrying until the roll has a scoring trick,
    // then returns the dice the greedy MachinePlayer would keep (the highest-scoring trick).
    // Rolls use real randomness; a full Farkle (nothing scores) is uncommon, so a few attempts
    // makes the setup reliable while keeping the dice — and the assertions — real.
    private async Task<(int gameId, int player1, IReadOnlyList<int> bestKeep)> StartGameAndRollBestKeepAsync()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var gameId = (await _client.Api.Games.PostAsync())!.Id!.Value;

            var player1 = (await _client.Api.Games[gameId].Players.PostAsync(
                new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "David" }))?.Id ?? 0;
            await _client.Api.Games[gameId].Players.PostAsync(
                new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = "Allison" });
            await _client.Api.Games[gameId].Start.PostAsync(
                new FarkleContractsHttpRequests_BeginGameRequest { PlayerId = 1 });

            var roll = await _client.Api.Games[gameId].Players[player1].Rolls.PostAsync();
            var dice = (roll!.DiceValues ?? []).Select(v => v ?? 0).ToList();
            var bestKeep = MachinePlayer.ChooseBestKeep(dice);
            if (bestKeep.Count > 0) return (gameId, player1, bestKeep);
        }

        throw new InvalidOperationException("No scoring trick was rolled after 10 attempts.");
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

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast PlayerJoined within 5 seconds");

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

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5_000));
        Assert.True(completed == tcs.Task, "Hub did not broadcast GameBegan within 5 seconds");

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
