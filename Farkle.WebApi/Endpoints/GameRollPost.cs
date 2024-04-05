using Eventuous;
using Farkle.WebApi.Application;
using FastEndpoints;
using Greedy.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;

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
    logger.LogInformation("ℹ️ In game post fast endpoint");
    var command = new Command.RollDice(req.GameId, req.PlayerId);

    var result = await new GameService(store).Handle(command, ct);

    // TODO: react
      await SendResultAsync(TypedResults.Ok(result));
  }
}
