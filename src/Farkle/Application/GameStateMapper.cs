using Farkle.Domain.GameAggregate;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Application;

internal static class GameStateMapper
{
  public static GameStateResponse ToGameState(GameState s) =>
    new(
      GameId: s.Id!.Id,
      Stage: s.GameStage.ToString(),
      CurrentPlayerId: s.PlayerInTurn,
      // Host = the first player who joined. `Players` rotates each turn (front = current
      // player), so use the lowest id (ids are assigned sequentially from 1) rather than Players[0].
      HostPlayerId: s.Players.IsEmpty ? 0 : s.Players.Min(p => p.Id),
      TurnScore: s.TurnScore.Value,
      Scoreboard: s.Players
        .Select(p => new PlayerScore(p.Id, p.Name, s.ScoreTable.GetValueOrDefault(p.Id, 0), p.Color))
        .ToArray(),
      Winner: s.Winner is null
        ? null
        : new WinnerResponse(s.Winner.Id, s.Winner.Name, s.ScoreTable.GetValueOrDefault(s.Winner.Id, 0)),
      TableCenter: s.TableCenter.Select(d => d.Value).ToArray(),
      DiceKept: s.DiceKept.Select(d => d.Value).ToArray(),
      DiceSetAside: s.DiceSetAside.Select(d => d.Value).ToArray(),
      TurnNumber: s.TurnNumber);
}
