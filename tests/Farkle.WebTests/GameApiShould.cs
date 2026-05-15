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
        adapter.BaseUrl = (_httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty) + "/api";
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

    private Task StartGameAsync(int gameId)
        => _client.Games.PostAsync(
            new FarkleContractsHttpRequests_StartGameRequest { Id = gameId });

    private Task JoinGameAsync(int gameId, int playerId, string playerName)
        => _client.Games[gameId].Players[playerId].PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = playerName });

    private Task RollDiceAsync(int gameId, int playerId)
        => _client.Games[gameId].Players[playerId].Rolls.PostAsync();

    private Task KeepDiceAsync(int gameId, int playerId, int[] dice)
        => _client.Games[gameId].Players[playerId].Keeps.PostAsync(
            new FarkleContractsHttpRequests_KeepDiceRequest
            {
                DiceValues = dice.Select(v => (int?)v).ToList()
            });
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
