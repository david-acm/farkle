using Ardalis.Result;
using Farkle.Spa.Components;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Spa.Services;

public interface IGameService
{
  public Task<Result<IList<DiceValue>>> RollDiceAsync(int   gameId, int playerId);
  public Task<int>                      StartGameAsync(int  gameId);
  public Task                           JoinPlayerAsync(int gameId, int playerId, string playerName);
  public Task<KeepDiceResponse> KeepDiceAsync(int   gameId, int playerId, IEnumerable<int> diceToKeep);
}
