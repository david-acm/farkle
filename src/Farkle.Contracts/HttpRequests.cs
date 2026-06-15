namespace Farkle.Contracts;

// [AggregateCommands<Game>]
public static class HttpRequests
{
  // [HttpCommand(Route = "players")]
  public record JoinPlayerRequest(int GameId, string PlayerName);

  // [HttpCommand(Route = "start")]
  public record BeginGameRequest(int GameId, int PlayerId);

  // [HttpCommand(Route = "diceRolls")]
  public record RollDiceRequest(int GameId, int PlayerId);

  // [HttpCommand(Route = "diceKeeps")]
  public record KeepDiceRequest(int GameId, int PlayerId, IEnumerable<int> DiceValues);

  // #159 — transient keep selection: set a single rolled die aside / put it back.
  public record SetDiceAsideRequest(int GameId, int PlayerId, int DieValue);

  public record ReturnDiceRequest(int GameId, int PlayerId, int DieValue);

  // [HttpCommand(Route = "turnPasses")]
  public record PassTurnHttp(int GameId, int PlayerId);

  // GET /api/games/{gameId} — full state snapshot for refresh / reconnect.
  public record GetGameRequest(int GameId);
}
