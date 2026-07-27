using HotDice.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static HotDice.Contracts.HttpRequests;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.SetDiceAside;

// SetDiceAside slice endpoint (#159, #303). Moves a single rolled die into the transient set-aside
// selection and broadcasts it so spectators see the live selection; Keep remains the commit.
public static class SetDiceAsideEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/setasides")]
  public static (Results<Ok<SetAsideResponse>, ProblemHttpResult>, Events, GameNotifications.TableChanged?) Post(
    int gameId, int playerId, SetDiceAsideRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state) =>
    SliceOutcome.From(
      state,
      SetDiceAsideDecider.Decide(
        new SetDiceAsideCommand(gameId, playerId, DieValue.FromValue(body.DieValue)), state),
      s => new SetAsideResponse(s.Code, s.DiceSetAside.Select(d => d.Value).ToArray()),
      new GameNotifications.TableChanged(gameId));
}
