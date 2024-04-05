using Eventuous;
using Farkle.WebApi.Application;
using FastEndpoints;
using Greedy.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Result=Ardalis.Result.Result;

namespace Farkle.WebApi.Endpoints;

public class GamePlayerPost(
  ILogger<GamePost> logger,
  IAggregateStore store) : Endpoint<V1.JoinPlayerHttp, Result>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}");
  }

  public override async Task HandleAsync(V1.JoinPlayerHttp req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ In game post fast endpoint");
    var command = new Command.JoinPlayer(req.GameId, req.PlayerId, req.PlayerName);

    var result = await new GameService(store).Handle(command, ct);
    
    if (result is ErrorResult<GameState> error)
    {
      var responseAction = error.Exception switch
      {
        DomainException e => (Func<Task>)(() => SendResultAsync(TypedResults.Conflict(e.Message))),
        _                 => () => SendResultAsync(TypedResults.StatusCode(500))
      };
      await responseAction();
      return;
    }

    await SendOkAsync(ct);
  }
}
