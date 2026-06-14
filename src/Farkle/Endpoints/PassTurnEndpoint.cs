using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class PassTurnEndpoint(
  ILogger<PassTurnEndpoint>  logger,
  IGameService               service)
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

    // The TurnChanged broadcast now fires from the Eventuous subscription
    // (GameBroadcastHandler) after the event is committed — the endpoint only returns
    // the HTTP response (#88).
    var result = await service
      .HandleAsync<Command.PassTurn, PassTurnResponse>(command, ct,
        s => PassTurnMapper.ToPassTurnResponse(s, req.PlayerId));

    await SendResultAsync(result);
  }
}
