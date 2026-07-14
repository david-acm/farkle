using Farkle.Domain.GameAggregate;
using Farkle.Features.StartGame;
using Marten;
using Marten.Exceptions;
using Npgsql;

namespace Farkle.Application;

public interface IGameCreator
{
  Task<int> CreateGameAsync(CancellationToken cancellationToken);
}

// Owns the "create a new game" use case: allocate a fresh random game id and retry if it collides
// with an existing game. Starts a new Marten event stream keyed "game-{code}" (ADR 0004); a
// collision surfaces as Marten's ExistingStreamIdCollisionException (same-session) or a Postgres
// unique-violation (23505) at save, so we draw another id and retry — the Marten analogue of the
// old optimistic-concurrency-on-create check.
internal sealed class GameCreator(IDocumentStore store, IGameIdGenerator idGenerator) : IGameCreator
{
  private const int MaxAttempts = 10;

  public async Task<int> CreateGameAsync(CancellationToken cancellationToken)
  {
    for (var attempt = 0; attempt < MaxAttempts; attempt++)
    {
      var id = idGenerator.Next();
      try
      {
        await using var session = store.LightweightSession();
        session.Events.StartStream<GameState>(
          $"game-{id}",
          StartGameDecider.Decide(new StartGameCommand(id), new GameState()).ToArray());
        await session.SaveChangesAsync(cancellationToken);
        return id;
      }
      catch (ExistingStreamIdCollisionException)
      {
        // id already taken — draw another and retry.
      }
      catch (PostgresException pg) when (pg.SqlState == PostgresErrorCodes.UniqueViolation)
      {
        // Concurrent creation of the same id — draw another and retry.
      }
    }

    throw new InvalidOperationException($"Could not allocate a unique game id after {MaxAttempts} attempts.");
  }
}
