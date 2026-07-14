using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.JoinPlayer;

// JoinPlayer slice endpoint (#303). Adds a player to an existing game's lobby, then broadcasts the
// updated roster to everyone in the game.
public static class JoinPlayerEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/players")]
  public static (Results<Ok<JoinPlayerResponse>, ProblemHttpResult>, Events, GameNotifications.LobbyChanged?) Post(
    int gameId, JoinPlayerRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state) =>
    SliceOutcome.From(
      state,
      JoinPlayerDecider.Decide(new JoinPlayerCommand(gameId, body.PlayerName), state),
      s =>
      {
        var lobby = LobbyMapper.ToLobbyState(s);
        return new JoinPlayerResponse(
          s.Players.Last().Id, s.PlayerInTurn, lobby.HostPlayerId, lobby.Stage, lobby.Roster);
      },
      new GameNotifications.LobbyChanged(gameId));
}
