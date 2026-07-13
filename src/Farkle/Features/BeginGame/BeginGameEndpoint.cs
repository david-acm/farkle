using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using Wolverine.Marten;
using static Farkle.Contracts.HttpRequests;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.BeginGame;

// BeginGame slice endpoint (#303). The host moves the game out of the lobby into play; broadcasts the
// lobby → in-play transition to everyone in the game.
public static class BeginGameEndpoint
{
  public static string StreamId(int gameId) => $"game-{gameId}";

  [WolverinePost("/api/games/{gameId:int}/start")]
  public static (Results<Ok<LobbyStateResponse>, ProblemHttpResult>, Events, GameNotifications.GameBegan?) Post(
    int gameId, BeginGameRequest body,
    [WriteAggregate(FromMethod = nameof(StreamId))] GameState state) =>
    SliceOutcome.From(
      state,
      BeginGameDecider.Decide(new Command.BeginGame(gameId, body.PlayerId), state),
      LobbyMapper.ToLobbyState,
      new GameNotifications.GameBegan(gameId));
}
