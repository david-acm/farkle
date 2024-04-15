using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Farkle.Spa.Components;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Spa.Services;

public class GameService : IGameService
{
  private readonly HttpClient           _gameClient;
  private readonly ILogger<GameService> _logger;
  
  public GameService(HttpClient gameClient, ILogger<GameService> logger)
  {
    _gameClient  = gameClient;
    _logger = logger;
  }

  public async Task<IList<DiceValue>> RollDiceAsync(int gameId, int playerId)
  {
    var result =
      await _gameClient.PostAsJsonAsync($"http://localhost:8000/api/games/{gameId}/players/{playerId}/rolls",
        new { GameId = gameId, PlayerId = playerId });

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    var stringContent = await result.Content.ReadAsStringAsync();
    var response      = JsonSerializer.Deserialize<RollDiceResponse>(stringContent, options);

    var tableCenter = response!.Dice.Select(DiceValue.FromValue).ToList();
    return tableCenter ?? new List<DiceValue>();
  }

  public async Task JoinPlayerAsync(int gameId, int playerId, string playerName)
  {
    await _gameClient.PostAsJsonAsync($"http://localhost:8000/api/games/{gameId}/players/{playerId}",
      new { PlayerName = playerName });
  }
  public async Task<IDictionary<int, int>> KeepDiceAsync(int gameId, int playerId, IEnumerable<int> diceToKeep)
  {
    var result = await _gameClient.PostAsJsonAsync($"http://localhost:8000/api/games/{gameId}/players/{playerId}/keeps", 
      new
      { 
        DiceValues = diceToKeep
      });
    
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    
    var stringContent = await result.Content.ReadAsStringAsync();
    var response      = JsonSerializer.Deserialize<KeepDiceResponse>(stringContent, options);
    
    return default!;
  }
  
  public async Task<int> StartGameAsync(int gameId)
  {
    // TODO: remove hardcoded value
    var result = await _gameClient.PostAsJsonAsync("http://localhost:8000/api/games", new { Id = gameId });
    
    var asString = await result.Content.ReadAsStringAsync();
    var response     = JsonSerializer.Deserialize<StartGameResponse>(asString, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true
    });
    _logger.LogInformation($"Received game started response string: {asString}");
    _logger.LogInformation($"Received game started response with id: {response!.Id}");
    
    return response!.Id;
  }
}
