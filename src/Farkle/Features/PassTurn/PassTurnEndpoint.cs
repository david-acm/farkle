using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.PassTurn;

// PassTurn slice endpoint (#303). Locks in the turn score and rotates to the next player (or ends the
// game on a win), then broadcasts the turn change — the original real-time multiplayer trigger.
// Both ids come from the route, so there is no request body.
public static class PassTurnEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/turns")]
  public static (Results<Ok<PassTurnResponse>, ProblemHttpResult>, Events, GameNotifications.TurnChanged?) Post(
    int gameId, int playerId,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state)
  {
    var events = PassTurnDecider.Decide(new Command.PassTurn(gameId, playerId), state).ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    var response = PassTurnMapper.ToPassTurnResponse(GameState.Fold(state, events), playerId);
    return (TypedResults.Ok(response), new Events(events), new GameNotifications.TurnChanged(gameId, playerId));
  }
}
