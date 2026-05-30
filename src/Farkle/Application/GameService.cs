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

    On<Command.BeginGame>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.BeginGame(cmd));

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

  // Attempts to start a brand-new game with the supplied id. Reports whether the id
  // was free (Created) or already in use (IdAlreadyInUse) so the caller can decide on a
  // retry policy; genuine failures are thrown. The retry/id-allocation policy lives in
  // GameCreator — this method just delegates the StartGame command to the domain.
  public async Task<StartGameOutcome> TryStartGameAsync(int id, CancellationToken cancellationToken)
  {
    var result = await Handle(new Command.StartGame(new GameId(id)), cancellationToken);
    return result switch
    {
      OkResult<GameState>                                                        => StartGameOutcome.Created,
      ErrorResult<GameState> e when e.Exception is OptimisticConcurrencyException => StartGameOutcome.IdAlreadyInUse,
      ErrorResult<GameState> e                                                    => throw e.Exception ?? new InvalidOperationException(e.Message),
      _                                                                          => throw new InvalidOperationException("Unexpected result creating game.")
    };
  }

  // TODO: Generalize this method
  public async Task<IResult> HandleAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken, Func<GameState,TResponse>? mapper = null)
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
      return result.ToMinimalApiResult<GameState, TResponse>(mapper);
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
  Task<StartGameOutcome> TryStartGameAsync(int id, CancellationToken cancellationToken);

  Task<IResult> HandleAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken, Func<GameState,TResponse>? mapper = null)
    where TResponse : class
    where TCommand : class;
}

internal enum StartGameOutcome
{
  Created,
  IdAlreadyInUse
}
