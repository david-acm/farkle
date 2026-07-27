using HotDice.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static HotDice.Contracts.HttpRequests;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.ReturnDice;

// ReturnDice slice endpoint (#159, #303). Puts a previously set-aside die back into the roll — the
// companion to SetDiceAside. Transient selection change; broadcasts the table change to spectators.
public static class ReturnDiceEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/putbacks")]
  public static (Results<Ok<SetAsideResponse>, ProblemHttpResult>, Events, GameNotifications.TableChanged?) Post(
    int gameId, int playerId, ReturnDiceRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state) =>
    SliceOutcome.From(
      state,
      ReturnDiceDecider.Decide(
        new ReturnDiceCommand(gameId, playerId, DieValue.FromValue(body.DieValue)), state),
      s => new SetAsideResponse(s.Code, s.DiceSetAside.Select(d => d.Value).ToArray()),
      new GameNotifications.TableChanged(gameId));
}
