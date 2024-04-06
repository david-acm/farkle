using Farkle.Spa.Components;

namespace Farkle.Spa.Services;

public interface IGameService
{
  public Task<IList<DiceValue>> RollDiceAsync(int   gameId, int playerId);
  public Task                   StartGameAsync(int  gameId);
  public Task                   JoinPlayerAsync(int gameId, int playerId, string playerName);
}
