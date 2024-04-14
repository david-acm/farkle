using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Farkle.Spa.Components;
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
      await _gameClient.PostAsJsonAsync("http://localhost:8000/diceRolls",
        new { GameId = gameId, PlayerId = playerId });

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    var stringContent = await result.Content.ReadAsStringAsync();
    var response      = JsonSerializer.Deserialize<CommandResponse>(stringContent, options);

    var tableCenter = response?.State?.TableCenter?.Select(v => DiceValue.FromValue(int.Parse($"{v.Value}"))).ToList();
    return tableCenter ?? new List<DiceValue>();
  }

  public async Task JoinPlayerAsync(int gameId, int playerId, string playerName)
  {
    await _gameClient.PostAsJsonAsync("http://localhost:8000/players",
      new { GameId = gameId, PlayerId = playerId, PlayerName = playerName });
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
