using Eventuous;
using FastEndpoints;
using Farkle.GameAggregate;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using GameService=Farkle.WebApi.Application.GameService;
using IResult=Microsoft.AspNetCore.Http.IResult;
using ProblemDetails=FastEndpoints.ProblemDetails;

namespace Farkle.WebApi.Endpoints;

public class GameRollPost(
  ILogger<GameRollPost> logger,
  IAggregateStore store)
  : Endpoint<V1.RollDiceHttp,
    Results<Ok<IResult>,
      ProblemDetails>>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}/rolls");
  }

  public override async Task HandleAsync(V1.RollDiceHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ In game roll post fast endpoint");
    var command = new Command.RollDice(req.GameId, req.PlayerId);

    Result<GameState> result = await new GameService(store).HandleAsync(command, ct);

    var minimalResult = result.AsMinimalResult();

    await SendResultAsync(minimalResult);
  }
}

