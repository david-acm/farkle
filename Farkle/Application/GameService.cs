using Eventuous;
using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Farkle.SharedKernel;
using static Eventuous.ExpectedState;

namespace Farkle.Application;

internal class GameService
  : CommandService<Game, GameState, GameId>,
    IGameService
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
  public async Task<IResult> HandleAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken)
    where TResponse : class
    where TCommand : class
  {
    var result = await base.Handle(command, cancellationToken);
    
    // TODO: Refactor this. Check if event is and
    var errorEvents = result.Changes?
      .Where(c => c.Event is IErrorEvent)
      .Select(e => e.Event.GetType().Name)
      .ToList() ?? [];
    if (!errorEvents.Any())
    {
      return result.ToMinimalApiResult<GameState, TResponse>();
    }

    var message = string.Concat(errorEvents);

    var errorResult = new ErrorResult<GameState>(
      $"Error handling command {command.GetType().Name}", 
      new DomainException(message))
      {};
    
    return errorResult.ToMinimalApiResult<GameState, TResponse>();

  }
}

internal interface IGameService
{
  Task<IResult> HandleAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken)
    where TResponse : class
    where TCommand : class;
}
