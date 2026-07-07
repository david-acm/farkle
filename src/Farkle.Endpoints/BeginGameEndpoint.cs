using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class BeginGameEndpoint(
  ILogger<BeginGameEndpoint> logger,
  IMessageBus                bus)
  : TypedEndpoint<BeginGameRequest, LobbyStateResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/start");
  }

  public override async Task HandleAsync(BeginGameRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId}. Player {playerId} starting play", req.GameId, req.PlayerId);
    var result = await bus.InvokeAsync<Result<LobbyStateResponse>>(
      new Command.BeginGame(req.GameId, req.PlayerId), ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
