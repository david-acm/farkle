using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// #159 — put a previously set-aside die back (companion to set aside). Transient,
// persisted + broadcast selection change; never keeps or scores dice.
internal class ReturnDiceEndpoint(
  ILogger<ReturnDiceEndpoint> logger,
  IMessageBus                 bus,
  GameNotifier                notifier)
  : TypedEndpoint<ReturnDiceRequest, SetAsideResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/putbacks");
  }

  public override async Task HandleAsync(ReturnDiceRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId} player {PlayerId} putting die {Die} back",
      req.GameId, req.PlayerId, req.DieValue);

    var command = new Command.ReturnDice(req.GameId, req.PlayerId, DieValue.FromValue(req.DieValue));
    var result = await bus.InvokeAsync<Result<SetAsideResponse>>(command, ct);
    if (result.IsSuccess) await notifier.TableChangedAsync(req.GameId, ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
