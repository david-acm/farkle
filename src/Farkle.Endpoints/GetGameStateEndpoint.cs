using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Marten;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// GET /api/games/{gameId} — returns the full game-state snapshot so a client can restore its view
// after a page refresh / reconnect. CQRS read side (ADR 0004): GameState is the Marten Inline
// snapshot, so this queries it directly by the "game-{code}" key — read-your-own-writes, no separate
// projection store. 404 when the game stream doesn't exist. Live play is pushed over SignalR.
internal class GetGameStateEndpoint(
  ILogger<GetGameStateEndpoint> logger,
  IQuerySession                 query)
  : TypedEndpoint<GetGameRequest, GameStateResponse>
{
  public override void Configure()
  {
    Get("/api/games/{gameId}");
  }

  public override async Task HandleAsync(GetGameRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Loading state snapshot", req.GameId);

    var state = await query.LoadAsync<GameState>($"game-{req.GameId}", ct);
    if (state is null)
    {
      await Send.NotFoundAsync(ct);
      return;
    }

    await Send.OkAsync(GameStateMapper.ToGameState(state), ct);
  }
}
