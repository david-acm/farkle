using System.Net.Http.Headers;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using WebApp.Client.Services.Generated;
using WebApp.Client.Services.Generated.Models;

namespace Farkle.WebTests;

public class GameApiShould : IClassFixture<GameApiWebAppFactory>
{
    private readonly HttpClient      _httpClient;
    private readonly FarkleApiClient _client;

    public GameApiShould(GameApiWebAppFactory factory)
    {
        _httpClient = factory.CreateClient();
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

        await _client.Api.Auth.Register.PostAsync(
            new WebAppAuthRegisterRequest { Email = email, Password = password });

        var login = await _client.Api.Auth.Login.PostAsync(
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
            () => _client.Api.Games.PostAsync(
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
        => _client.Api.Games.PostAsync(
            new FarkleContractsHttpRequests_StartGameRequest { Id = gameId });

    private Task JoinGameAsync(int gameId, int playerId, string playerName)
        => _client.Api.Games[gameId].Players[playerId].PostAsync(
            new FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = playerName });

    private Task RollDiceAsync(int gameId, int playerId)
        => _client.Api.Games[gameId].Players[playerId].Rolls.PostAsync();

    private Task KeepDiceAsync(int gameId, int playerId, int[] dice)
        => _client.Api.Games[gameId].Players[playerId].Keeps.PostAsync(
            new FarkleContractsHttpRequests_KeepDiceRequest
            {
                DiceValues = dice.Select(v => (int?)v).ToList()
            });
}
