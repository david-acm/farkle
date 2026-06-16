using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// GET /api/games/{gameId} — returns the full game-state snapshot so a client can restore
// its view after a page refresh / reconnect. CQRS read side (#156): reads the GameView
// projection (IGameViewStore) and only falls back to replaying the aggregate when the view
// isn't present yet (a brand-new game not projected, or a host with the read model disabled).
// The view is updated asynchronously, so this read is eventually consistent — fine for a
// refresh path (live play is pushed over SignalR, not polled through here).
internal class GetGameStateEndpoint(
  ILogger<GetGameStateEndpoint> logger,
  IGameService                  service,
  IGameViewStore                viewStore)
  : TypedEndpoint<GetGameRequest, GameStateResponse>
{
  public override void Configure()
  {
    Get("/api/games/{gameId}");
  }

  public override async Task HandleAsync(GetGameRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Loading state snapshot", req.GameId);

    var json = await viewStore.GetAsync(req.GameId, ct);
    if (json is not null)
    {
      await Send.OkAsync(GameStateMapper.ToGameState(GameStateSerializer.Deserialize(json)), ct);
      return;
    }

    // View absent — fall back to a stream replay (covers not-yet-projected games and
    // read-model-disabled hosts). Still 404 when the game itself doesn't exist.
    var state = await service.LoadStateAsync(new GameId(req.GameId), ct);
    if (state is null)
    {
      await Send.NotFoundAsync(ct);
      return;
    }

    await Send.OkAsync(GameStateMapper.ToGameState(state), ct);
  }
}
