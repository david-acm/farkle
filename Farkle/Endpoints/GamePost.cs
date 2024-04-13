using Eventuous;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Result=Ardalis.Result.Result;

namespace Farkle.Endpoints;

internal class GamePost(
  ILogger<GamePost> logger,
  IAggregateStore store) : Endpoint<HttpRequests.StartGameHttp,
                                    Results<Ok<Result>,
                                    ProblemDetails>>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games");
    Description(d => d.Accepts<HttpRequests.StartGameHttp>(), clearDefaults: true);
  }

  public override async Task HandleAsync(HttpRequests.StartGameHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ In game post fast endpoint");
    var result = await new GameService(store).HandleAsync(new Command.StartGame(req.Id), ct);

    var minimalResult = result.AsMinimalResult();

    await SendResultAsync(minimalResult);
  }
}
