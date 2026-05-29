using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Microsoft.Extensions.Logging;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal class JoinPlayerEndpoint(
  ILogger<JoinPlayerEndpoint> logger,
  IGameService                service,
  IGameEventBroadcaster       broadcaster)
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

    // Capture the lobby snapshot via closure — set only on success (mapper not called on error).
    LobbyStateResponse? lobby = null;
    var result = await service
      .HandleAsync<Command.JoinPlayer, JoinPlayerResponse>(command, ct,
        s =>
        {
          lobby = LobbyMapper.ToLobbyState(s);
          return new JoinPlayerResponse(
            s.Players.Last().Id,
            s.PlayerInTurn,
            lobby.HostPlayerId,
            lobby.Stage,
            lobby.Roster);
        });

    if (lobby is not null)
    {
      try
      {
        await broadcaster.BroadcastPlayerJoinedAsync(lobby, ct);
      }
      catch (Exception ex)
      {
        logger.LogWarning(ex, "Failed to broadcast PlayerJoined for game {GameId}", req.GameId);
      }
    }

    await SendResultAsync(result);
  }
}
