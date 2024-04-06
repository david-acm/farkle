using Eventuous;
using Farkle.GameAggregate;
using static Eventuous.ExpectedState;

namespace Farkle.WebApi.Application;

public class GameService
  : CommandService<Game, GameState, GameId>
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
      .Execute((game, cmd) => game.RollDiceV2(cmd));

    On<Command.KeepDice>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.KeepDice(cmd));

    On<Command.PassTurn>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.PassTurn(cmd));
  }

  // TODO: Generalize this method
  public async Task<Result<GameState>> HandleAsync<TCommand>(TCommand command, CancellationToken cancellationToken) where TCommand : class
  {
    var result = await base.Handle(command, cancellationToken);

    var errorEvents = result.Changes?
      .Where(c => c.Event is IErrorEvent)
      .Select(e => e.Event.GetType().Name)
      .ToList() ?? [];
    if (!errorEvents.Any())
    {
      return result;
    }

    var message = string.Concat(errorEvents);

    var errorResult = new ErrorResult<GameState>(
      $"Error handling command {typeof(TCommand).Name}", 
      new DomainException(message))
      {};
    
    return errorResult;

  }
}
