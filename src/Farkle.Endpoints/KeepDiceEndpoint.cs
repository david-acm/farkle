using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class KeepDiceEndpoint(
  ILogger<KeepDiceEndpoint> logger,
  IMessageBus               bus)
  : TypedEndpoint<KeepDiceRequest, KeepDiceResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/keeps");
  }

  public override async Task HandleAsync(KeepDiceRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId} Keeping dice for {PlayerId}", req.GameId, req.PlayerId);
    var command = new Command.KeepDice(req.GameId, req.PlayerId, req.DiceValues.Select(DieValue.FromValue));
    var result = await bus.InvokeAsync<Result<KeepDiceResponse>>(command, ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
