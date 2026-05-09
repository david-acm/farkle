using Ardalis.Result;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services.Generated;
using KiotaModels = WebApp.Client.Services.Generated.Models;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public class GameService : IGameService
{
  private readonly FarkleApiClient      _client;
  private readonly ILogger<GameService> _logger;

  public GameService(FarkleApiClient client, ILogger<GameService> logger)
  {
    _client = client;
    _logger = logger;
  }

  public async Task<Result<IList<DieValue>>> RollDiceAsync(int gameId, int playerId)
  {
    var response = await _client.Api.Games[gameId].Players[playerId].Rolls.PostAsync();
    var dice     = response?.DiceValues?.Select(v => DieValue.FromValue(v ?? 0)).ToList() ?? new List<DieValue>();
    return dice;
  }

  public async Task JoinPlayerAsync(int gameId, int playerId, string playerName)
  {
    await _client.Api.Games[gameId].Players[playerId].PostAsync(
      new KiotaModels.FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = playerName });
  }

  public async Task<KeepDiceResponse> KeepDiceAsync(int gameId, int playerId, IEnumerable<int> diceToKeep)
  {
    var response = await _client.Api.Games[gameId].Players[playerId].Keeps.PostAsync(
      new KiotaModels.FarkleContractsHttpRequests_KeepDiceRequest { DiceValues = diceToKeep.Select(v => (int?)v).ToList() });
    _logger.LogInformation("KeepDice response: {TurnScore}", response?.TurnScore);
    return new KeepDiceResponse(response?.Id ?? 0, response?.TurnScore ?? 0);
  }

  public async Task<int> StartGameAsync(int gameId)
  {
    var response = await _client.Api.Games.PostAsync(new KiotaModels.FarkleContractsHttpRequests_StartGameRequest { Id = gameId });
    _logger.LogInformation("Started game with id: {Id}", response?.Id);
    return response?.Id ?? 0;
  }

  public async Task<PassTurnResponse> PassTurnAsync(int gameId, int playerId)
  {
    var response = await _client.Api.Games[gameId].Players[playerId].Turns.PostAsync();
    _logger.LogInformation("PassTurn response: gameId={GameId} newScore={NewScore}", response?.GameId, response?.NewScore);
    var winner = response?.Winner is { } w ? new WinnerResponse(w.PlayerId ?? 0, w.Name ?? string.Empty, w.Score ?? 0) : null;
    return new PassTurnResponse(response?.GameId ?? 0, response?.PlayerId ?? 0, response?.NewScore ?? 0, winner);
  }
}
