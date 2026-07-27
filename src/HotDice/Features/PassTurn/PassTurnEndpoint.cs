using HotDice.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.PassTurn;

// PassTurn slice endpoint (#303). Locks in the turn score and rotates to the next player (or ends the
// game on a win), then broadcasts the turn change — the original real-time multiplayer trigger.
// Both ids come from the route, so there is no request body.
public static class PassTurnEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players/{playerId:int}/turns")]
  public static (Results<Ok<PassTurnResponse>, ProblemHttpResult>, Events, GameNotifications.TurnChanged?) Post(
    int gameId, int playerId,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state) =>
    SliceOutcome.From(
      state,
      PassTurnDecider.Decide(new PassTurnCommand(gameId, playerId), state),
      s => PassTurnMapper.ToPassTurnResponse(s, playerId),
      new GameNotifications.TurnChanged(gameId, playerId));
}
