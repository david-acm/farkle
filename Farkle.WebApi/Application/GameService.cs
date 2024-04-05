using Eventuous;
using Greedy.GameAggregate;
using static Eventuous.ExpectedState;

namespace Farkle.WebApi.Application;

public class GameService : CommandService<Game, GameState, GameId>
{
  public GameService(IAggregateStore store) : base(store)
  {
    On<Command.StartGame>()
      .InState(New)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.Start(cmd));

    On<Command.JoinPlayer>()
      .GetId(cmd => new GameId(cmd.GameId))
      .InState(Existing)
      .Execute((game, cmd) => game.JoinPlayer(cmd));

    On<Command.RollDice>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Act((game, cmd) => game.RollDiceV2(cmd));

    On<Command.KeepDice>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.KeepDice(cmd));

    On<Command.PassTurn>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.PassTurn(cmd));
  }
}
