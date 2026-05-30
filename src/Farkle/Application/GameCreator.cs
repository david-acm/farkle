namespace Farkle.Application;

internal interface IGameCreator
{
  Task<int> CreateGameAsync(CancellationToken cancellationToken);
}

// Owns the "create a new game" use case: allocate a fresh random game id and retry
// if it collides with an existing game. The actual StartGame command is delegated to
// the command service (IGameService), keeping retry/id-allocation policy out of both
// the command-routing service and the domain.
internal sealed class GameCreator(IGameService commands, IGameIdGenerator idGenerator) : IGameCreator
{
  private const int MaxAttempts = 10;

  public async Task<int> CreateGameAsync(CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < MaxAttempts; attempt++)
    {
      var id      = idGenerator.Next();
      var outcome = await commands.TryStartGameAsync(id, cancellationToken);

      if (outcome == StartGameOutcome.Created)
        return id;

      // StartGameOutcome.IdAlreadyInUse — draw another id and try again.
    }

    throw new InvalidOperationException($"Could not allocate a unique game id after {MaxAttempts} attempts.");
  }
}
