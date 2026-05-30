using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// GET /api/games/{gameId} — returns the full game-state snapshot so a client can
// restore its view after a page refresh / reconnect. Read-only: replays the
// aggregate's event stream via the store (no command, no new events).
internal class GetGameStateEndpoint(
  ILogger<GetGameStateEndpoint> logger,
  IGameService                  service)
  : TypedEndpoint<GetGameRequest, GameStateResponse>
{
  public override void Configure()
  {
    Get("/api/games/{gameId}");
  }

  public override async Task HandleAsync(GetGameRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Loading state snapshot", req.GameId);

    var state = await service.LoadStateAsync(new GameId(req.GameId), ct);
    if (state is null)
    {
      await SendNotFoundAsync(ct);
      return;
    }

    await SendOkAsync(GameStateMapper.ToGameState(state), ct);
  }
}
