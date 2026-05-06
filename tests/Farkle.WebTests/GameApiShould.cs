using Farkle.Endpoints;
using FastEndpoints;
using static Farkle.Endpoints.StartGame;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.WebTests;

public class GameApiShould : IClassFixture<GameApiWebAppFactory>
{
  private readonly HttpClient _client;
  
  public GameApiShould(GameApiWebAppFactory factory)
  {
    _client = factory.CreateClient();
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
