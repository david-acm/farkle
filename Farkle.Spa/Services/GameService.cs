using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ardalis.Result;
using Farkle.Spa.Components;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Spa.Services;

public class GameService : IGameService
{
  private readonly HttpClient           _gameClient;
  private readonly ILogger<GameService> _logger;
  
  public GameService(HttpClient gameClient, ILogger<GameService> logger)
  {
    _gameClient = gameClient;
    _logger     = logger;
  }
  
  public async Task<Result<IList<DiceValue>>> RollDiceAsync(int gameId, int playerId)
  {
    var result =
      await _gameClient.PostAsJsonAsync($"http://localhost:8000/api/games/{gameId}/players/{playerId}/rolls",
        new
        {
          GameId = gameId, PlayerId = playerId
        });
    
    var options = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    };
    var stringContent = await result.Content.ReadAsStringAsync();
    if (result.StatusCode == HttpStatusCode.BadRequest)
    {
      var error = JsonSerializer.Deserialize<ProblemDetails>(stringContent, options);
      return Result.Error(error?.Detail);
    }
    
    var response = JsonSerializer.Deserialize<RollDiceResponse>(stringContent, options);
    
    var tableCenter = response?.Dice?.Select(DiceValue.FromValue).ToList();
    return tableCenter ?? new List<DiceValue>();
  }
  
  public async Task JoinPlayerAsync(int gameId, int playerId, string playerName)
  {
    await _gameClient.PostAsJsonAsync($"http://localhost:8000/api/games/{gameId}/players/{playerId}",
      new
      {
        PlayerName = playerName
      });
  }
  public async Task<KeepDiceResponse> KeepDiceAsync(int gameId, int playerId, IEnumerable<int> diceToKeep)
  {
    var result = await _gameClient.PostAsJsonAsync($"http://localhost:8000/api/games/{gameId}/players/{playerId}/keeps",
      new
      {
        DiceValues = diceToKeep
      });
    
    var options = new JsonSerializerOptions
    {
      PropertyNameCaseInsensitive = true
    };
    
    var stringContent = await result.Content.ReadAsStringAsync();
    _logger.LogInformation("String content received from keepdice endpoint: {stringContent}", stringContent);
    var response = JsonSerializer.Deserialize<KeepDiceResponse>(stringContent, options);
    
    // TODO: Change this for a Result
    return response!;
  }
  
  public async Task<int> StartGameAsync(int gameId)
  {
    // TODO: remove hardcoded value
    var result = await _gameClient.PostAsJsonAsync("http://localhost:8000/api/games", new
    {
      Id = gameId
    });
    
    var asString = await result.Content.ReadAsStringAsync();
    var response = JsonSerializer.Deserialize<StartGameResponse>(asString, new JsonSerializerOptions()
    {
      PropertyNameCaseInsensitive = true
    });
    _logger.LogInformation($"Received game started response string: {asString}");
    _logger.LogInformation($"Received game started response with id: {response!.Id}");
    
    return response!.Id;
  }
}

public record ProblemDetails(string Type, string Title, int Status, string Detail);
