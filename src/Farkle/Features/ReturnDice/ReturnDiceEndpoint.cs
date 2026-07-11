using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.ReturnDice;

// ReturnDice slice endpoint (#159, #303). Puts a previously set-aside die back into the roll — the
// companion to SetDiceAside. Transient selection change; broadcasts the table change to spectators.
public static class ReturnDiceEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId}/players/{playerId}/putbacks")]
  public static (Results<Ok<SetAsideResponse>, ProblemHttpResult>, Events, GameNotifications.TableChanged?) Post(
    int gameId, int playerId, ReturnDiceRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state)
  {
    var command = new Command.ReturnDice(gameId, playerId, DieValue.FromValue(body.DieValue));
    var events  = ReturnDiceDecider.Decide(command, state).ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    var s = GameState.Fold(state, events);
    return (TypedResults.Ok(new SetAsideResponse(s.Code, s.DiceSetAside.Select(d => d.Value).ToArray())),
            new Events(events), new GameNotifications.TableChanged(gameId));
  }
}
