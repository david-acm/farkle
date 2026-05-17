using Ardalis.Result;
using Farkle.ApiClient;
using WebApp.Client.Pages.Game.Components;
using KiotaModels = Farkle.ApiClient.Models;
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

  public async Task<int> JoinPlayerAsync(int gameId, string playerName)
  {
    var response = await _client.Api.Games[gameId].Players.PostAsync(
      new KiotaModels.FarkleContractsHttpRequests_JoinPlayerRequest { PlayerName = playerName });
    _logger.LogInformation("JoinPlayer response: assignedId={Id}", response?.Id);
    return response?.Id ?? 0;
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
    _logger.LogInformation("PassTurn response: newScore={NewScore}", response?.NewScore);
    return ToPassTurnResponse(response);
  }

  private static PassTurnResponse ToPassTurnResponse(KiotaModels.FarkleContractsHttpResponses_PassTurnResponse? r)
  {
    var scoreboard = r?.Scoreboard?
      .Select(p => new PlayerScore(p.PlayerId ?? 0, p.Name ?? "", p.Score ?? 0))
      .ToArray();
    var winner = r?.Winner is null ? null
      : new WinnerResponse(r.Winner.PlayerId ?? 0, r.Winner.Name ?? "", r.Winner.Score ?? 0);
    return new PassTurnResponse(
      r?.GameId ?? 0, r?.PlayerId ?? 0, r?.NewScore ?? 0,
      winner, r?.CurrentPlayerId ?? 0, scoreboard);
  }
}
