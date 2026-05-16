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

        // Player 1 rolls and keeps a scoring die to accumulate a non-zero turn score.
        var roll1        = await _client.Games[gameId].Players[player1].Rolls.PostAsync();
        var scoringDice1 = (roll1!.DiceValues ?? []).Where(v => v == 1 || v == 5).ToList();
        if (scoringDice1.Count > 0)
        {
            var kept = await KeepDiceAsync(gameId, player1, [(int)scoringDice1[0]!]);
            Assert.True(kept!.TurnScore > 0, "turn score should be positive after keeping a scoring die");
        }

        await PassTurnAsync(gameId, player1);

        // Player 2's turn score must start from 0, not carry over from player 1.
        var roll2        = await _client.Games[gameId].Players[player2].Rolls.PostAsync();
        var scoringDice2 = (roll2!.DiceValues ?? []).Where(v => v == 1 || v == 5).ToList();
        if (scoringDice2.Count > 0)
        {
            var kept2 = await KeepDiceAsync(gameId, player2, [(int)scoringDice2[0]!]);
            // A single die scores exactly 100 (for a 1) or 50 (for a 5).
            // If turn score had carried over from player 1 it would be larger.
            var expected = scoringDice2[0] == 1 ? 100 : 50;
            Assert.Equal(expected, kept2!.TurnScore);
        }
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

    private Task PassTurnAsync(int gameId, int playerId)
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
