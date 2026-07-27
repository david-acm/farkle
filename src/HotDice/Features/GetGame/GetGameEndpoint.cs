using HotDice.Domain.GameAggregate;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.GetGame;

// GetGame slice endpoint (#303). CQRS read side (ADR 0004): GameState is the Marten Inline snapshot,
// so this queries it directly by the "game-{code}" key — read-your-own-writes, no separate projection
// store. 404 when the game stream doesn't exist. Live play is pushed over SignalR, not polled here.
public static class GetGameEndpoint
{
  [WolverineGet("/api/games/{gameId:int}")]
  public static async Task<Results<Ok<GameStateResponse>, NotFound>> Get(
    int gameId, IQuerySession query, CancellationToken ct)
  {
    var state = await query.LoadAsync<GameState>($"game-{gameId}", ct);
    return state is null
      ? TypedResults.NotFound()
      : TypedResults.Ok(GameStateMapper.ToGameState(state));
  }
}
