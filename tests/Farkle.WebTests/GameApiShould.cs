using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farkle.Endpoints;
using FastEndpoints;
using WebApp.Auth;
using static Farkle.Endpoints.StartGame;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.WebTests;

public class GameApiShould : IClassFixture<GameApiWebAppFactory>
{
  private readonly HttpClient _client;
  private readonly GameApiWebAppFactory _factory;

  public GameApiShould(GameApiWebAppFactory factory)
  {
    _factory = factory;
    _client = factory.CreateClient();
    AuthenticateClientAsync().GetAwaiter().GetResult();
  }

  private async Task AuthenticateClientAsync()
  {
    var email = $"test-{Guid.NewGuid():N}@farkle.dev";
    const string password = "Test@123!";

    var registerResponse = await _client.POSTAsync<RegisterEndpoint, RegisterRequest>(
      new RegisterRequest(email, password));
    registerResponse.EnsureSuccessStatusCode();

    var (loginResponse, result) = await _client.POSTAsync<LoginEndpoint, LoginRequest, LoginResponse>(
      new LoginRequest(email, password));
    loginResponse.EnsureSuccessStatusCode();

    _client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", result!.Token);
  }

  [Fact]
  public async Task RejectUnauthenticatedRequestsAsync()
  {
    var savedAuth = _client.DefaultRequestHeaders.Authorization;
    _client.DefaultRequestHeaders.Authorization = null;

    var (response, _) = await _client
      .POSTAsync<StartGameEndpoint, StartGameRequest, StartGameResponse>(new(999));

    _client.DefaultRequestHeaders.Authorization = savedAuth;

    Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task AllowPlayerToRollDiceAsync()
  {
    // Arrange
    const int gameId = 208;

    // Act
    // Assert
    await StartGameAsync(gameId);
    await JoinGameAsync(gameId, 1, "David");
    await JoinGameAsync(gameId, 1, "Allison");
    await RollDiceAsync(gameId);
    await KeepDiceAsync(gameId, 1, [1]);
  }

  private async Task KeepDiceAsync(int gameId, int playerId, int[] dice)
    => await _client.POSTAsync<KeepDiceEndpoint, KeepDiceRequest, KeepDiceResponse>(new(gameId, playerId, dice));
  private async Task RollDiceAsync(int gameId)
    => await _client.POSTAsync<RollDiceEndpoint, RollDiceRequest, RollDiceResponse>(new(gameId, 1));
  private async Task JoinGameAsync(int gameId, int playerId, string playerName)
    => await _client.POSTAsync<JoinPlayerEndpoint, JoinPlayerRequest, JoinPlayerResponse>(new(gameId, playerId, playerName));
  private async Task StartGameAsync(int gameId)
    => await _client.POSTAsync<StartGameEndpoint, StartGameRequest, StartGameResponse>(new(gameId));
}
