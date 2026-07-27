using HotDice.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static HotDice.Contracts.HttpRequests;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.KeepDice;

// KeepDice slice endpoint (#303). Commits the in-turn player's set-aside dice into their hand and
// updates the turn score; broadcasts the table change to spectators.
public static class KeepDiceEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/keeps")]
  public static (Results<Ok<KeepDiceResponse>, ProblemHttpResult>, Events, GameNotifications.TableChanged?) Post(
    int gameId, int playerId, KeepDiceRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state) =>
    SliceOutcome.From(
      state,
      KeepDiceDecider.Decide(
        new KeepDiceCommand(gameId, playerId, body.DiceValues.Select(DieValue.FromValue)), state),
      s => new KeepDiceResponse(s.Code, s.TurnScore),
      new GameNotifications.TableChanged(gameId));
}
