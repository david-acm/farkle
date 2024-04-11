using Eventuous;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Result=Ardalis.Result.Result;

namespace Farkle.Endpoints;

internal class GamePlayerPost(
  ILogger<GamePost> logger,
  IAggregateStore store) : Endpoint<HttpRequests.JoinPlayerHttp, Result>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}");
  }

  public override async Task HandleAsync(HttpRequests.JoinPlayerHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ In game post fast endpoint");
    var command = new Command.JoinPlayer(req.GameId, req.PlayerId, req.PlayerName);

    var result = await new GameService(store).HandleAsync(command, ct);

    var minimalResult = result.AsMinimalResult();

    await SendResultAsync(minimalResult);
  }
}
