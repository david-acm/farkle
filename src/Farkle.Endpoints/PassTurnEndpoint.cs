using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class PassTurnEndpoint(
  ILogger<PassTurnEndpoint>  logger,
  IMessageBus                bus)
  : TypedEndpoint<PassTurnHttp, PassTurnResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/turns");
  }

  public override async Task HandleAsync(PassTurnHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId}. Player {playerId} passing turn", req.GameId, req.PlayerId);
    var result = await bus.InvokeAsync<Result<PassTurnResponse>>(
      new Command.PassTurn(req.GameId, req.PlayerId), ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
