using Farkle.Application;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

public static class StartGame
{
  internal class StartGameEndpoint(
    ILogger<StartGameEndpoint> logger,
    IGameCreator gameCreator)
    : EndpointWithoutRequest<StartGameResponse>
  {
    public override void Configure()
    {
      Post("/api/games");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
      var id = await gameCreator.CreateGameAsync(ct);
      logger.LogInformation("ℹ️ Created game: {gameId}", id);
      await SendAsync(new StartGameResponse(id));
    }
  }
}
