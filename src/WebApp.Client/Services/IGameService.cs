using Ardalis.Result;
using WebApp.Client.Pages.Game.Components;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Services;

public interface IGameService
{
  public Task<Result<IList<DieValue>>> RollDiceAsync(int   gameId, int playerId);
  public Task<int>                      StartGameAsync(int  gameId);
  public Task                           JoinPlayerAsync(int gameId, int playerId, string playerName);
  public Task<KeepDiceResponse>  KeepDiceAsync(int  gameId, int playerId, IEnumerable<int> diceToKeep);
  public Task<PassTurnResponse>  PassTurnAsync(int  gameId, int playerId);
}
