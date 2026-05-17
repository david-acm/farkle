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
    Post("/api/games/{gameId}/players");
  }

  public override async Task HandleAsync(JoinPlayerRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game: {gameId}. Joining player with name: {playerName}", req.GameId, req.PlayerName);
    var command = new Command.JoinPlayer(req.GameId, req.PlayerName);

    var result = await service
      .HandleAsync<Command.JoinPlayer, JoinPlayerResponse>(command, ct,
        s => new JoinPlayerResponse(s.Players.Last().Id, s.PlayerInTurn));

    await SendResultAsync(result);
  }
}
