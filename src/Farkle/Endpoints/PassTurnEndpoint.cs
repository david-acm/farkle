using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class PassTurnEndpoint(
  ILogger<PassTurnEndpoint>  logger,
  IGameService               service,
  IGameEventBroadcaster      broadcaster)
  : TypedEndpoint<PassTurnHttp, PassTurnResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/turns");
  }

  public override async Task HandleAsync(PassTurnHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId}. Player {playerId} passing turn", req.GameId, req.PlayerId);
    var command = new Command.PassTurn(req.GameId, req.PlayerId);

    // Capture the mapped response via closure — set only on success (mapper not called on error).
    PassTurnResponse? broadcast = null;
    var result = await service
      .HandleAsync<Command.PassTurn, PassTurnResponse>(command, ct,
        s =>
        {
          var r = new PassTurnResponse(
            s.Id!.Id,
            req.PlayerId,
            s.GameScoreFor(req.PlayerId),
            s.Winner == null ? null : new WinnerResponse(s.Winner.Id, s.Winner.Name, s.GameScoreFor(s.Winner.Id)),
            s.PlayerInTurn,
            s.Players.Select(p => new PlayerScore(p.Id, p.Name, s.GameScoreFor(p.Id))).ToArray());
          broadcast = r;
          return r;
        });

    if (broadcast is not null)
    {
      try
      {
        await broadcaster.BroadcastTurnChangedAsync(broadcast, ct);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to broadcast TurnChanged for game {GameId}", req.GameId);
      }
    }

    await SendResultAsync(result);
  }
}
