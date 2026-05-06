using Farkle.Application;
using Farkle.Domain.GameAggregate;
using FastEndpoints;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

public static class StartGame
{
  
  
  internal class StartGameEndpoint(
    ILogger<StartGameEndpoint> logger,
    IGameService service)
    : TypedEndpoint<StartGameRequest, StartGameResponse>
  {
    
    public override void Configure()
    {
      Post("/api/games");
      // Description(d => d.Accepts<StartGameRequest>("application/json"), clearDefaults: true);
    }
    
    public override async Task HandleAsync(StartGameRequest req, CancellationToken ct)
    {
      logger.LogInformation("ℹ️ Starting game: {gameId}", req.Id);
      
      // TODO : Remove generics from here
      var result = await service
        .HandleAsync<Command.StartGame, StartGameResponse>(new Command.StartGame(req.Id), ct);
      
      
      await SendResultAsync(result);
    }
  }
}
