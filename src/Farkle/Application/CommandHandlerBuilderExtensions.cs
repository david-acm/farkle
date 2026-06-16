using Eventuous;

namespace Farkle.Application;

public static class CommandHandlerBuilderExtensions
{
  // 0.15.1 dropped the CommandServiceDelegates.ActOnAggregate delegate; the fluent terminal is now
  // IDefineExecution<...>.Act(Action<TAggregate, TCommand>), reached through ICommandHandlerBuilder.
  public static void Execute<TCommand, TAggregate, TState, TId>(
    this ICommandHandlerBuilder<TCommand, TAggregate, TState, TId> builder,
    Action<TAggregate, TCommand> action)
    where TCommand : class
    where TAggregate : Aggregate<TState>, new()
    where TState : State<TState>, new()
    where TId : Id
  {
    builder.Act((game, cmd) =>
    {
      try
      {
        action(game, cmd);
      }
      catch (DomainException)
      {
        // We ignore the domain exceptions because otherwise the error events would not be persisted to the store. In a future version these events will be handled and will return the appropriate HTTP 400 Bad Request response
      }
    });
  }
}
