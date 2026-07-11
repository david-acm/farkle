using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.KeepDice;

// KeepDice slice endpoint (#303). Commits the in-turn player's set-aside dice into their hand and
// updates the turn score; broadcasts the table change to spectators.
public static class KeepDiceEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/keeps")]
  public static (Results<Ok<KeepDiceResponse>, ProblemHttpResult>, Events, GameNotifications.TableChanged?) Post(
    int gameId, int playerId, KeepDiceRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state)
  {
    var command = new Command.KeepDice(gameId, playerId, body.DiceValues.Select(DieValue.FromValue));
    var events  = KeepDiceDecider.Decide(command, state).ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    var s = GameState.Fold(state, events);
    return (TypedResults.Ok(new KeepDiceResponse(s.Code, s.TurnScore)),
            new Events(events), new GameNotifications.TableChanged(gameId));
  }
}
