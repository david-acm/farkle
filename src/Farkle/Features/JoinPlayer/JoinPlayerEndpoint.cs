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

  [WolverinePost("/api/games/{gameId}/players")]
  public static (Results<Ok<JoinPlayerResponse>, ProblemHttpResult>, Events, GameNotifications.LobbyChanged?) Post(
    int gameId, JoinPlayerRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state)
  {
    var events = JoinPlayerDecider.Decide(new Command.JoinPlayer(gameId, body.PlayerName), state).ToArray();

    if (events.OfType<IErrorEvent>().FirstOrDefault() is { } error)
      return (TypedResults.Problem(statusCode: 400, title: error.GetType().Name), new Events(), null);

    var s     = GameState.Fold(state, events);
    var lobby = LobbyMapper.ToLobbyState(s);
    var response = new JoinPlayerResponse(
      s.Players.Last().Id, s.PlayerInTurn, lobby.HostPlayerId, lobby.Stage, lobby.Roster);
    return (TypedResults.Ok(response), new Events(events), new GameNotifications.LobbyChanged(gameId));
  }
}
