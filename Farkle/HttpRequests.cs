using Eventuous.AspNetCore.Web;
using Farkle.Domain.GameAggregate;

namespace Farkle;

[AggregateCommands<Game>]
internal static class HttpRequests
{
  [HttpCommand(Route = "games")]
  public record StartGameHttp(int Id);

  [HttpCommand(Route = "players")]
  public record JoinPlayerHttp(int GameId, int PlayerId, string PlayerName);

  [HttpCommand(Route = "diceRolls")]
  public record RollDiceHttp(int GameId, int PlayerId);

  [HttpCommand(Route = "diceKeeps")]
  public record KeepDiceHttp(int GameId, int PlayerId, int[] DiceValues);

  [HttpCommand(Route = "turnPasses")]
  public record PassTurnHttp(int GameId, int PlayerId);
}
