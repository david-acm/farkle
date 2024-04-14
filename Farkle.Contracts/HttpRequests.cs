namespace Farkle.Contracts;

// [AggregateCommands<Game>]
public static class HttpRequests
{
  // [HttpCommand(Route = "games")]
  public record StartGameRequest(int Id);

  // [HttpCommand(Route = "players")]
  public record JoinPlayerRequest(int GameId, int PlayerId, string PlayerName);

  // [HttpCommand(Route = "diceRolls")]
  public record RollDiceRequest(int GameId, int PlayerId);

  // [HttpCommand(Route = "diceKeeps")]
  public record KeepDiceRequest(int GameId, int PlayerId, int[] DiceValues);

  // [HttpCommand(Route = "turnPasses")]
  public record PassTurnHttp(int GameId, int PlayerId);
}
