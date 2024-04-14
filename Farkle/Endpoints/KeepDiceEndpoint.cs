using Farkle.Application;
using Farkle.Contracts;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;

namespace Farkle.Endpoints;


internal class KeepDiceEndpoint(
  ILogger<RollDiceEndpoint> logger,
  IGameService              service)
  : TypedEndpoint<RollDiceRequest, HttpResponses.KeepDiceResponse>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}/keeps");
  }
  
  public override async Task HandleAsync(RollDiceRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId} Keeping dice for {PlayerId}", req.GameId, req.PlayerId);
    var command = new Command.RollDice(req.GameId, req.PlayerId);
    
    // TODO: Inject game service or use mediatr
    var result = await service
      .HandleAsync<Command.RollDice, HttpResponses.KeepDiceResponse>(command, ct);
    
    await SendResultAsync(result);
  }
}
