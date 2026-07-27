using Ardalis.Result;
using Farkle.ApiClient;
using Microsoft.Kiota.Abstractions;
using Farkle.Ui.Pages.Game.Components;
using KiotaModels = Farkle.ApiClient.Models;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Ui.Services;

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

  public async Task<JoinPlayerResponse> JoinPlayerAsync(int gameId, string playerName)
  {
    var response = await _client.Api.Games[gameId].Players.PostAsync(
      new KiotaModels.JoinPlayerRequest { PlayerName = playerName });
    _logger.LogInformation("JoinPlayer: assignedId={Id} currentPlayer={CurrentPlayerId}",
      response?.Id, response?.CurrentPlayerId);
    return new JoinPlayerResponse(
      response?.Id ?? 0,
      response?.CurrentPlayerId ?? 0,
      response?.HostPlayerId ?? 0,
      response?.Stage ?? "",
      ToRoster(response?.Roster));
  }

  public async Task<LobbyStateResponse> BeginGameAsync(int gameId, int playerId)
  {
    var response = await _client.Api.Games[gameId].Start.PostAsync(
      new KiotaModels.BeginGameRequest { PlayerId = playerId });
    _logger.LogInformation("BeginGame: game={GameId} stage={Stage}", gameId, response?.Stage);
    return new LobbyStateResponse(
      response?.GameId ?? gameId,
      response?.Stage ?? "",
      ToRoster(response?.Roster) ?? [],
      response?.HostPlayerId ?? 0,
      response?.CurrentPlayerId ?? 0,
      response?.TurnNumber ?? 0);
  }

  private static IReadOnlyList<LobbyPlayer>? ToRoster(
    List<KiotaModels.LobbyPlayer>? roster) =>
    roster?.Select(p => new LobbyPlayer(p.PlayerId ?? 0, p.Name ?? "", p.Color ?? "")).ToArray();

  public async Task<KeepDiceResponse> KeepDiceAsync(int gameId, int playerId, IEnumerable<int> diceToKeep)
  {
    var response = await _client.Api.Games[gameId].Players[playerId].Keeps.PostAsync(
      new KiotaModels.KeepDiceRequest { DiceValues = diceToKeep.Select(v => (int?)v).ToList() });
    _logger.LogInformation("KeepDice response: {TurnScore}", response?.TurnScore);
    return new KeepDiceResponse(response?.Id ?? 0, response?.TurnScore ?? 0);
  }

  public async Task SetDiceAsideAsync(int gameId, int playerId, int dieValue)
  {
    await _client.Api.Games[gameId].Players[playerId].Setasides.PostAsync(
      new KiotaModels.SetDiceAsideRequest { DieValue = dieValue });
    _logger.LogDebug("SetDiceAside: game={GameId} player={PlayerId} die={Die}", gameId, playerId, dieValue);
  }

  public async Task ReturnDiceAsync(int gameId, int playerId, int dieValue)
  {
    await _client.Api.Games[gameId].Players[playerId].Putbacks.PostAsync(
      new KiotaModels.ReturnDiceRequest { DieValue = dieValue });
    _logger.LogDebug("ReturnDice: game={GameId} player={PlayerId} die={Die}", gameId, playerId, dieValue);
  }

  public async Task<int> CreateGameAsync()
  {
    var response = await _client.Api.Games.PostAsync();
    _logger.LogInformation("Created game with id: {Id}", response?.Id);
    return response?.Id ?? 0;
  }

  public async Task<PassTurnResponse> PassTurnAsync(int gameId, int playerId)
  {
    var response = await _client.Api.Games[gameId].Players[playerId].Turns.PostAsync();
    _logger.LogInformation("PassTurn response: newScore={NewScore}", response?.NewScore);
    return ToPassTurnResponse(response);
  }

  public async Task<GameStateResponse?> GetGameStateAsync(int gameId)
  {
    KiotaModels.GameStateResponse? r;
    try
    {
      r = await _client.Api.Games[gameId].GetAsync();
    }
    catch (ApiException ex) when (ex.ResponseStatusCode == 404)
    {
      // Game no longer exists — caller treats null as "clear the stale session".
      _logger.LogInformation("GetGameState: game {GameId} not found", gameId);
      return null;
    }

    if (r is null) return null;
    _logger.LogInformation("GetGameState: game={GameId} stage={Stage} current={CurrentPlayerId}",
      r.GameId, r.Stage, r.CurrentPlayerId);
    return new GameStateResponse(
      r.GameId ?? 0,
      r.Stage ?? "",
      r.CurrentPlayerId ?? 0,
      r.HostPlayerId ?? 0,
      r.TurnScore ?? 0,
      Scoreboard: (r.Scoreboard ?? [])
        .Select(p => new PlayerScore(p.PlayerId ?? 0, p.Name ?? "", p.Score ?? 0, p.Color ?? ""))
        .ToArray(),
      // The nullable WinnerResponse is emitted as a oneOf, so Kiota nests it under WinnerResponse.
      Winner: r.Winner?.WinnerResponse is not { } w
        ? null
        : new WinnerResponse(w.PlayerId ?? 0, w.Name ?? "", w.Score ?? 0),
      TableCenter: (r.TableCenter ?? []).Select(v => v ?? 0).ToArray(),
      DiceKept: (r.DiceKept ?? []).Select(v => v ?? 0).ToArray(),
      DiceSetAside: (r.DiceSetAside ?? []).Select(v => v ?? 0).ToArray(),
      TurnNumber: r.TurnNumber ?? 0);
  }

  private static PassTurnResponse ToPassTurnResponse(KiotaModels.PassTurnResponse? r)
  {
    var scoreboard = r?.Scoreboard?
      .Select(p => new PlayerScore(p.PlayerId ?? 0, p.Name ?? "", p.Score ?? 0, p.Color ?? ""))
      .ToArray();
    var winner = r?.Winner?.WinnerResponse is not { } w ? null
      : new WinnerResponse(w.PlayerId ?? 0, w.Name ?? "", w.Score ?? 0);
    return new PassTurnResponse(
      r?.GameId ?? 0, r?.PlayerId ?? 0, r?.NewScore ?? 0,
      winner, r?.CurrentPlayerId ?? 0, scoreboard, r?.TurnNumber ?? 0);
  }
}
