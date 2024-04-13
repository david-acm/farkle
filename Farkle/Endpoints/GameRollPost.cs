using Eventuous;
using Farkle.Domain.GameAggregate;
using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Application_GameService=Farkle.Application.GameService;
using IResult=Microsoft.AspNetCore.Http.IResult;
using ProblemDetails=FastEndpoints.ProblemDetails;

namespace Farkle.Endpoints;

internal class GameRollPost(
  ILogger<GameRollPost> logger,
  IAggregateStore store)
  : Endpoint<HttpRequests.RollDiceHttp,
    Results<Ok<IResult>,
      ProblemDetails>>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}/rolls");
  }

  public override async Task HandleAsync(HttpRequests.RollDiceHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ In game roll post fast endpoint");
    var command = new Command.RollDice(req.GameId, req.PlayerId);

    Result<GameState> result = await new Application_GameService(store).HandleAsync(command, ct);

    var minimalResult = result.AsMinimalResult();

    await SendResultAsync(minimalResult);
  }
}

