using Ardalis.Result;
using HotDice.Ui.Pages.Game.Components;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Ui.Services;

public interface IGameService
{
  public Task<Result<IList<DieValue>>> RollDiceAsync(int   gameId, int playerId);
  public Task<int>                      CreateGameAsync();
  public Task<JoinPlayerResponse>       JoinPlayerAsync(int gameId, string playerName);
  public Task<LobbyStateResponse>       BeginGameAsync(int  gameId, int playerId);
  public Task<KeepDiceResponse> KeepDiceAsync(int     gameId, int playerId, IEnumerable<int> diceToKeep);
  public Task<PassTurnResponse> PassTurnAsync(int     gameId, int playerId);

  // #159 — transient keep selection: set a single die aside / put it back.
  public Task SetDiceAsideAsync(int gameId, int playerId, int dieValue);
  public Task ReturnDiceAsync(int   gameId, int playerId, int dieValue);

  // Full-state snapshot for restoring the view on refresh / reconnect.
  // Returns null when the game does not exist (HTTP 404).
  public Task<GameStateResponse?> GetGameStateAsync(int gameId);
}
