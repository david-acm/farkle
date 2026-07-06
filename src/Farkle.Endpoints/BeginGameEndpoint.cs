using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class BeginGameEndpoint(
  ILogger<BeginGameEndpoint> logger,
  IGameService               service)
  : TypedEndpoint<BeginGameRequest, LobbyStateResponse>
{
  public override void Configure()
  {
    Post("/api/games/{gameId}/start");
  }

  public override async Task HandleAsync(BeginGameRequest req, CancellationToken ct)
  {
    logger.LogInformation("ℹ️ Game {gameId}. Player {playerId} starting play", req.GameId, req.PlayerId);
    var command = new Command.BeginGame(req.GameId, req.PlayerId);

    // The GameBegan broadcast now fires from the Eventuous subscription
    // (GameBroadcastHandler) after the event is committed — the endpoint only returns
    // the HTTP response (#88).
    var result = await service
      .HandleAsync<Command.BeginGame, LobbyStateResponse>(command, ct,
        LobbyMapper.ToLobbyState);

    await Send.ResultAsync(result);
  }
}
