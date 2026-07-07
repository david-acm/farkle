using Ardalis.Result;
using Ardalis.Result.AspNetCore;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using Wolverine;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class JoinPlayerEndpoint(
  ILogger<JoinPlayerEndpoint> logger,
  IMessageBus                 bus)
  : TypedEndpoint<JoinPlayerRequest, JoinPlayerResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/players");
  }

  public override async Task HandleAsync(JoinPlayerRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Joining player with name: {playerName}", req.GameId, req.PlayerName);
    var result = await bus.InvokeAsync<Result<JoinPlayerResponse>>(
      new Command.JoinPlayer(req.GameId, req.PlayerName), ct);
    await Send.ResultAsync(result.ToMinimalApiResult());
  }
}
