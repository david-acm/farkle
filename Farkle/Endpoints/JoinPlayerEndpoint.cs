using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class JoinPlayerEndpoint(
  ILogger<JoinPlayerEndpoint> logger,
  IGameService                service)
  : TypedEndpoint<JoinPlayerRequest, JoinPlayerResponse>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}");
  }
  
  public override async Task HandleAsync(JoinPlayerRequest req, CancellationToken ct)
  {
    // TODO: Use mediatr behaviors
    logger.LogInformation("ℹ️ Game: {gameId}. Joining player: {playerId}. With name: {playerName}", req.GameId, req.PlayerId, req.PlayerName);
    var command = new Command.JoinPlayer(req.GameId, req.PlayerId, req.PlayerName);
    
    var result = await service
      .HandleAsync<Command.JoinPlayer, JoinPlayerResponse>(command, ct);
    
    await SendResultAsync(result);
  }
}
