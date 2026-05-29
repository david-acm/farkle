using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class BeginGameEndpoint(
  ILogger<BeginGameEndpoint> logger,
  IGameService               service,
  IGameEventBroadcaster      broadcaster)
  : TypedEndpoint<BeginGameRequest, LobbyStateResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/start");
  }

  public override async Task HandleAsync(BeginGameRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId}. Player {playerId} starting play", req.GameId, req.PlayerId);
    var command = new Command.BeginGame(req.GameId, req.PlayerId);

    // Capture the mapped lobby via closure — set only on success (mapper not called on error).
    LobbyStateResponse? began = null;
    var result = await service
      .HandleAsync<Command.BeginGame, LobbyStateResponse>(command, ct,
        s =>
        {
          began = LobbyMapper.ToLobbyState(s);
          return began;
        });

    if (began is not null)
    {
      try
      {
        await broadcaster.BroadcastGameBeganAsync(began, ct);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to broadcast GameBegan for game {GameId}", req.GameId);
      }
    }

    await SendResultAsync(result);
  }
}
