using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

// #159 — set a single rolled die aside (transient keep selection). Persisted + broadcast
// so spectators see the in-turn player's selection live; Keep remains the commit.
internal class SetDiceAsideEndpoint(
  ILogger<SetDiceAsideEndpoint> logger,
  IMessageBus                   bus)
  : TypedEndpoint<SetDiceAsideRequest, SetAsideResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players/{playerId}/setasides");
  }

  public override async Task HandleAsync(SetDiceAsideRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId} player {PlayerId} setting die {Die} aside",
      req.GameId, req.PlayerId, req.DieValue);

    var command = new Command.SetDiceAside(req.GameId, req.PlayerId, DieValue.FromValue(req.DieValue));
    var result = await bus.InvokeAsync<Result<SetAsideResponse>>(command, ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
