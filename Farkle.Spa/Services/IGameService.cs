using Farkle.Spa.Components;

namespace Farkle.Spa.Services;

public interface IGameService
{
  public Task<IList<DiceValue>>      RollDiceAsync(int   gameId, int playerId);
  public Task<int>                   StartGameAsync(int  gameId);
  public Task                        JoinPlayerAsync(int gameId, int playerId, string playerName);
  public Task<IDictionary<int, int>> KeepDiceAsync(int   gameId, int playerId, IEnumerable<int> diceToKeep);
}
