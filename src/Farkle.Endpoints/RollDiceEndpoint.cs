using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class RollDiceEndpoint(
  ILogger<RollDiceEndpoint> logger,
  IMessageBus               bus,
  GameNotifier              notifier)
  : TypedEndpoint<RollDiceRequest, RollDiceResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/rolls");
  }

  public override async Task HandleAsync(RollDiceRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Rolling dice for player: {playerId}", req.GameId, req.PlayerId);
    var result = await bus.InvokeAsync<Result<RollDiceResponse>>(
      new Command.RollDice(req.GameId, req.PlayerId), ct);
    if (result.IsSuccess) await notifier.DiceRolledAsync(req.GameId, ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
