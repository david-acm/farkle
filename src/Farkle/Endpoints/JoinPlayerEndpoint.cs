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

    // The PlayerJoined broadcast now fires from the Eventuous subscription
    // (GameBroadcastHandler) after the event is committed — the endpoint only returns
    // the HTTP response (#88).
    var result = await service
      .HandleAsync<Command.JoinPlayer, JoinPlayerResponse>(command, ct,
        s =>
        {
          var lobby = LobbyMapper.ToLobbyState(s);
          return new JoinPlayerResponse(
            s.Players.Last().Id,
            s.PlayerInTurn,
            lobby.HostPlayerId,
            lobby.Stage,
            lobby.Roster);
        });

    await SendResultAsync(result);
  }
}
