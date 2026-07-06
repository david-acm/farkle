using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// #159 — put a previously set-aside die back (companion to set aside). Transient,
// persisted + broadcast selection change; never keeps or scores dice.
internal class ReturnDiceEndpoint(
  ILogger<ReturnDiceEndpoint> logger,
  IGameService                service)
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

    var result = await service
      .HandleAsync<Command.ReturnDice, SetAsideResponse>(command, ct,
        s => new SetAsideResponse(s.Id ?? 0, s.DiceSetAside.Select(d => d.Value).ToArray()));

    await Send.ResultAsync(result);
  }
}
