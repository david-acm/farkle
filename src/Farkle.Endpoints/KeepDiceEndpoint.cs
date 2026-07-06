using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;


internal class KeepDiceEndpoint(
  ILogger<RollDiceEndpoint> logger,
  IGameService              service)
  : TypedEndpoint<KeepDiceRequest, KeepDiceResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/keeps");
  }
  
  public override async Task HandleAsync(KeepDiceRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId} Keeping dice for {PlayerId}", req.GameId, req.PlayerId);
    var command = new Command.KeepDice(req.GameId, req.PlayerId, req.DiceValues.Select(d => DieValue.FromValue(d)));

    var result = await service
      .HandleAsync<Command.KeepDice, KeepDiceResponse>(command, ct,
        (s) => new KeepDiceResponse(s.Id ?? 0, s.TurnScore));
    
    await Send.ResultAsync(result);
  }
}
