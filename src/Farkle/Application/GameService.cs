using Eventuous;
using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using static Eventuous.ExpectedState;

namespace Farkle.Application;

internal class GameService
  : CommandService<Game, GameState, GameId>,
    IGameService
{
  private readonly IEventStore _store;

  public GameService(IEventStore store, IRandom random)
    : base(store, factoryRegistry: CreateAggregateFactory(random))
  {
    _store = store;

    On<Command.StartGame>()
      .InState(New)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.Start(cmd));

    On<Command.JoinPlayer>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
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

    On<Command.SetDiceAside>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.SetDiceAside(cmd));

    On<Command.ReturnDice>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.ReturnDice(cmd));

    On<Command.PassTurn>()
      .InState(Existing)
      .GetId(cmd => new GameId(cmd.GameId))
      .Execute((game, cmd) => game.PassTurn(cmd));
  }

  // #93: a per-service aggregate factory so Eventuous builds Game with the DI-provided
  // IRandom (the dice seam) on the command/write path, instead of the parameterless ctor's
  // DefaultRandomProvider. The read path (LoadAggregate, below) only replays events and never
  // rolls, so it can keep using the default global factory.
  private static AggregateFactoryRegistry CreateAggregateFactory(IRandom random)
  {
    var registry = new AggregateFactoryRegistry();
    registry.CreateAggregateUsing<Game, GameState>(() => new Game(random));
    return registry;
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
      { Success: true }                                       => StartGameOutcome.Created,
      { Exception: OptimisticConcurrencyException }            => StartGameOutcome.IdAlreadyInUse,
      { Exception: { } ex }                                    => throw ex,
      _                                                        => throw new InvalidOperationException("Unexpected result creating game.")
    };
  }

  // Read-only load of the current state by replaying the aggregate's event stream.
  // Returns null when the game stream doesn't exist yet (LoadAggregate yields an empty
  // aggregate whose State.Id is still the GameId.None sentinel — no GameStarted applied).
  public async Task<GameState?> LoadStateAsync(GameId gameId, CancellationToken cancellationToken)
  {
    // 0.15.1 retires IAggregateStore.LoadOrNew in favour of the IEventReader.LoadAggregate
    // extension. failIfNotFound:false yields an empty aggregate (State.Id == GameId.None) when
    // the game stream doesn't exist yet, preserving the previous null-on-missing contract.
    var game = await _store.LoadAggregate<Game, GameState, GameId>(
      gameId, failIfNotFound: false, cancellationToken: cancellationToken);
    return game.State.Id == GameId.None ? null : game.State;
  }

  public async Task<IResult> HandleAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken, Func<GameState,TResponse>? mapper = null)
    where TResponse : class
    where TCommand : class
  {
    var result = await base.Handle(command, cancellationToken);

    // 0.15.1: the emitted events live on the success case (Result<TState>.Ok.Changes).
    // Scan them for domain error events (IErrorEvent) so a rejected command becomes an HTTP error.
    var errorEvents = result.TryGet(out var ok)
      ? ok.Changes
        .Where(c => c.Event is IErrorEvent)
        .Select(e => e.Event.GetType().Name)
        .ToList()
      : [];
    if (!errorEvents.Any())
    {
      return result.ToMinimalApiResult<GameState, TResponse>(mapper);
    }

    var message = string.Concat(errorEvents);

    var errorResult = Eventuous.Result<GameState>.FromError(
      new DomainException(message),
      $"Error handling command {command.GetType().Name}");

    return errorResult.ToMinimalApiResult<GameState, TResponse>();
  }
}

internal interface IGameService
{
  Task<StartGameOutcome> TryStartGameAsync(int id, CancellationToken cancellationToken);

  Task<GameState?> LoadStateAsync(GameId gameId, CancellationToken cancellationToken);

  Task<IResult> HandleAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken, Func<GameState,TResponse>? mapper = null)
    where TResponse : class
    where TCommand : class;
}

internal enum StartGameOutcome
{
  Created,
  IdAlreadyInUse
}
