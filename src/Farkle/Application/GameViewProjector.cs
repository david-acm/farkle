using Eventuous;
using Eventuous.Subscriptions.Context;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;

namespace Farkle.Application;

/// <summary>
/// #156 — the CQRS read-side projector. Maintains a denormalized per-game view (the persisted
/// <see cref="GameState"/> snapshot) from the same Eventuous $all subscription the broadcaster
/// uses. It is <b>truly incremental</b>: per event it loads the stored snapshot, folds exactly
/// one event through the aggregate's own <c>When</c> logic, and stores the result — O(1) per
/// event, no replay, and zero duplication of the write-side fold. GET reads this view instead
/// of replaying the stream (see GetGameStateEndpoint).
/// </summary>
public sealed class GameViewProjector : Eventuous.Subscriptions.EventHandler
{
  private readonly IGameViewStore               _store;
  private readonly ILogger<GameViewProjector>   _logger;

  public GameViewProjector(IGameViewStore store, ILogger<GameViewProjector> logger)
  {
    _store  = store;
    _logger = logger;

    // Fold every state-changing Game event the same way — through GameState.When. Error
    // events (IErrorEvent) don't change state and have no handler, so they're intentionally
    // not projected (skipping them yields the same snapshot as an aggregate replay).
    On<GameEvents.V1.GameStarted>(Project);
    On<GameEvents.V1.PlayerJoined>(Project);
    On<GameEvents.V2.PlayerJoined>(Project);
    On<GameEvents.V1.GamePlayStarted>(Project);
    On<GameEvents.V1.DiceRolled>(Project);
    On<GameEvents.V2.DiceRolled>(Project);
    On<GameEvents.V1.DiceKept>(Project);
    On<GameEvents.V2.DiceKept>(Project);
    On<GameEvents.V1.DiceSetAside>(Project);
    On<GameEvents.V1.DiceReturned>(Project);
    On<GameEvents.V1.TurnPassed>(Project);
    On<GameEvents.V1.GameWon>(Project);
  }

  private async ValueTask Project<T>(MessageConsumeContext<T> ctx) where T : class
  {
    if (!TryGetGameId(ctx.Stream, out var gameId))
    {
      _logger.LogWarning("GameView projection skipped: could not parse game id from stream {Stream}", ctx.Stream);
      return;
    }

    // Load the prior snapshot (or an empty state for the first event — GameStarted), fold
    // this one event, and store. Reuses the aggregate's exact fold via State<T>.When.
    var json  = await _store.GetAsync(gameId, ctx.CancellationToken);
    var state = json is null ? new GameState() : GameStateSerializer.Deserialize(json);
    var next  = state.When(ctx.Message!);

    await _store.UpsertAsync(gameId, GameStateSerializer.Serialize(next), ctx.GlobalPosition, ctx.CancellationToken);
  }

  // Game events are written to the stream "Game-{id}" (StreamNameFactory). Pull the id back out.
  private static bool TryGetGameId(StreamName stream, out int gameId)
  {
    var name = stream.ToString();
    var dash = name.IndexOf('-');
    return int.TryParse(dash >= 0 ? name[(dash + 1)..] : name, out gameId);
  }
}
