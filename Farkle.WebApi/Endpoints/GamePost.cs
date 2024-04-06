using Eventuous;
using Farkle.Domain.GameAggregate;
using Farkle.WebApi.Application;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using static Microsoft.AspNetCore.Http.TypedResults;
using Result=Ardalis.Result.Result;

namespace Farkle.WebApi.Endpoints;

public class GamePost(
  ILogger<GamePost> logger,
  IAggregateStore store) : Endpoint<V1.StartGameHttp,
                                    Results<Ok<Result>,
                                    ProblemDetails>>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games");
    Description(d => d.Accepts<V1.StartGameHttp>(), clearDefaults: true);
  }

  public override async Task HandleAsync(V1.StartGameHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ In game post fast endpoint");
    var result = await new GameService(store).HandleAsync(new Command.StartGame(req.Id), ct);

    var minimalResult = result.AsMinimalResult();

    await SendResultAsync(minimalResult);
  }
}
