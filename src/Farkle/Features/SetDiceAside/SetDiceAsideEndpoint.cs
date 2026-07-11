using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.SetDiceAside;

// SetDiceAside slice endpoint (#159, #303). Moves a single rolled die into the transient set-aside
// selection and broadcasts it so spectators see the live selection; Keep remains the commit.
public static class SetDiceAsideEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/setasides")]
  public static (Results<Ok<SetAsideResponse>, ProblemHttpResult>, Events, GameNotifications.TableChanged?) Post(
    int gameId, int playerId, SetDiceAsideRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state)
  {
    var command = new Command.SetDiceAside(gameId, playerId, DieValue.FromValue(body.DieValue));
    var events  = SetDiceAsideDecider.Decide(command, state).ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    var s = GameState.Fold(state, events);
    return (TypedResults.Ok(new SetAsideResponse(s.Code, s.DiceSetAside.Select(d => d.Value).ToArray())),
            new Events(events), new GameNotifications.TableChanged(gameId));
  }
}
