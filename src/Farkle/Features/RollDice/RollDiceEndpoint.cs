using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.RollDice;

// RollDice slice endpoint (#303, ADR 0004 Option C). The endpoint IS the handler: Wolverine.HTTP
// binds the int game code from the route, [WriteAggregate(FromMethod)] maps it to the "game-{code}"
// Marten stream and loads GameState, the pure decider produces the events, and the tuple return
// appends them + writes the typed response. A precondition failure short-circuits with a 400
// ProblemDetails and appends nothing; success cascades a DiceRolled notification the outbox
// publishes post-commit for the SignalR broadcast.
public static class RollDiceEndpoint
{
  // Maps the route's int game code to the Marten stream key (string stream identity, ADR 0002/0004).
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/rolls")]
  public static (Results<Ok<RollDiceResponse>, ProblemHttpResult>, Events, GameNotifications.DiceRolled?) Post(
    int gameId, int playerId,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state,
    IRandom rng)
  {
    var roll   = Dice.FromNewRoll(rng, state.DiceToRoll);
    var events = RollDiceDecider.Decide(new Command.RollDice(gameId, playerId), state, roll).ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    var s = GameState.Fold(state, events);
    var response = new RollDiceResponse(s.Code, s.TableCenter.Select(d => d.Value).ToArray());
    return (TypedResults.Ok(response), new Events(events), new GameNotifications.DiceRolled(gameId));
  }
}
