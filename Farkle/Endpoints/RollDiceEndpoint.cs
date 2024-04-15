using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Mapster;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;


internal class RollDiceEndpoint(
  ILogger<RollDiceEndpoint> logger,
  IGameService              service)
  : TypedEndpoint<RollDiceRequest, RollDiceResponse>
{
  public override void Configure()
  {
    AllowAnonymous();
    Post("/api/games/{gameId}/players/{playerId}/rolls");
  }
  
  public override async Task HandleAsync(RollDiceRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Rolling dice for player: {playerId}", req.GameId, req.PlayerId);
    var command = new Command.RollDice(req.GameId, req.PlayerId);
    
    var result = await service
      .HandleAsync<Command.RollDice, RollDiceResponse>(command, ct, 
        (s) => new RollDiceResponse(s.Id!.Id, s.TableCenter.Select(d => d.Value).ToArray()));
    
    await SendResultAsync(result);
  }
  private static int[] ToArrayAsync(GameState s)
  {
    
    return s.TableCenter.Select(s => s.Value).ToArray();
  }
}
