using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using Farkle.WebTests.Generated;
using Farkle.WebTests.Generated.Models;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Farkle.WebTests;

public class GameApiShould : IClassFixture<GameApiWebAppFactory>
{
    private readonly HttpClient      _httpClient;
    private readonly FarkleApiClient _client;

    public GameApiShould(GameApiWebAppFactory factory)
    {
        // FastEndpoints requires Content-Type: application/json even on bodyless POSTs.
        // Wrap the factory client's handler to inject an empty JSON body when none is present,
        // mirroring the EmptyBodyJsonHandler used by the WASM client in production.
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
        var email    = $"test-{Guid.NewGuid():N}@farkle.dev";
        const string password = "Test@123!";

        await _client.Auth.Register.PostAsync(
            new WebAppAuthRegisterRequest { Email = email, Password = password });

        var login = await _client.Auth.Login.PostAsync(
            new WebAppAuthLoginRequest { Email = email, Password = password });

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", login!.Token);
    }

    [Fact]
    public async Task RejectUnauthenticatedRequestsAsync()
    {
        var savedAuth = _httpClient.DefaultRequestHeaders.Authorization;
        _httpClient.DefaultRequestHeaders.Authorization = null;

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => _client.Games.PostAsync(
                new FarkleContractsHttpRequests_StartGameRequest { Id = 999 }));

        _httpClient.DefaultRequestHeaders.Authorization = savedAuth;
        Assert.Equal(401, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task AllowPlayerToRollDiceAsync()
    {
        const int gameId = 208;

        await StartGameAsync(gameId);
        await JoinGameAsync(gameId, 1, "David");
        await JoinGameAsync(gameId, 1, "Allison");
        await RollDiceAsync(gameId, 1);
        await KeepDiceAsync(gameId, 1, [1]);
    }

    [Fact]
    public async Task PassTurnResetsTurnScoreAsync()
    {
        const int gameId  = 209;
        const int player1 = 1;
        const int player2 = 2;

        await StartGameAsync(gameId);
        await JoinGameAsync(gameId, player1, "David");
        await JoinGameAsync(gameId, player2, "Allison");

        // Player 1 keeps all their scoring dice to build a non-zero turn score.
        var roll1        = await _client.Games[gameId].Players[player1].Rolls.PostAsync();
        var scoringDice1 = (roll1!.DiceValues ?? []).Where(v => v == 1 || v == 5).Select(v => (int)v!).ToArray();
        int player1TurnScore = 0;
        if (scoringDice1.Length > 0)
        {
            var kept = await KeepDiceAsync(gameId, player1, scoringDice1);
            player1TurnScore = kept!.TurnScore ?? 0;
            Assert.True(player1TurnScore > 0, "turn score should be positive after keeping scoring dice");
        }

        // Passing locks the turn score into the player's cumulative game score.
        var pass = await PassTurnAsync(gameId, player1);
        Assert.NotNull(pass);
        Assert.Equal(player1TurnScore, pass.NewScore);

        // Player 2 can roll — confirming the game correctly advanced the turn with a fresh score.
        var roll2 = await _client.Games[gameId].Players[player2].Rolls.PostAsync();
        Assert.NotNull(roll2!.DiceValues);
        Assert.NotEmpty(roll2.DiceValues);
    }

    [Fact]
    public async Task RejectRollFromPlayerNotInTurnAsync()
    {
        const int gameId  = 301;
        const int player1 = 1;
        const int player2 = 2;

        await StartGameAsync(gameId);
        await JoinGameAsync(gameId, player1, "David");
        await JoinGameAsync(gameId, player2, "Allison");

        // Player 1 goes first; player 2 rolling out of turn must be rejected.
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => RollDiceAsync(gameId, player2));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task RejectDoubleRollWithoutKeepingAsync()
    {
        const int gameId  = 302;
        const int player1 = 1;
        const int player2 = 2;

        await StartGameAsync(gameId);
        await JoinGameAsync(gameId, player1, "David");
        await JoinGameAsync(gameId, player2, "Allison");

        // First roll always succeeds.
        await RollDiceAsync(gameId, player1);

        // A second roll before keeping any dice must be rejected.
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => RollDiceAsync(gameId, player1));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    [Fact]
    public async Task RejectPassWithoutRollingAsync()
    {
        const int gameId  = 303;
        const int player1 = 1;
        const int player2 = 2;

        await StartGameAsync(gameId);
        await JoinGameAsync(gameId, player1, "David");
        await JoinGameAsync(gameId, player2, "Allison");

        // Attempting to pass before rolling must be rejected.
        var ex = await Assert.ThrowsAsync<ApiException>(
            () => PassTurnAsync(gameId, player1));

        Assert.Equal(400, ex.ResponseStatusCode);
    }

    private Task StartGameAsync(int gameId)
        => _client.Games.PostAsync(
            new FarkleContractsHttpRequests_StartGameRequest { Id = gameId });

    private Task JoinGameAsync(int gameId, int playerId, string playerName)
        => _client.Games[gameId].Players[playerId].PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = playerName });

    private Task RollDiceAsync(int gameId, int playerId)
        => _client.Games[gameId].Players[playerId].Rolls.PostAsync();

    private Task<FarkleContractsHttpResponses_KeepDiceResponse?> KeepDiceAsync(int gameId, int playerId, int[] dice)
        => _client.Games[gameId].Players[playerId].Keeps.PostAsync(
            new FarkleContractsHttpRequests_KeepDiceRequest
            {
                DiceValues = dice.Select(v => (int?)v).ToList()
            });

    private Task<FarkleContractsHttpResponses_PassTurnResponse?> PassTurnAsync(int gameId, int playerId)
        => _client.Games[gameId].Players[playerId].Turns.PostAsync();
}

// FastEndpoints rejects POST requests with no Content-Type / body with 415.
// This handler injects an empty JSON body on bodyless POSTs so the Kiota client
// behaves the same way as the WASM production client (EmptyBodyJsonHandler).
file sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Method == HttpMethod.Post && request.Content == null)
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        return base.SendAsync(request, ct);
    }
}
