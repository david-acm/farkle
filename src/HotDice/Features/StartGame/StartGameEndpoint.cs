using HotDice.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Wolverine.Http;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features.StartGame;

// StartGame slice endpoint (#303). Creates a new game — the only slice with no existing aggregate to
// load, so it delegates to IGameCreator (which allocates a unique id and StartStreams the game with a
// collision retry). No broadcast: nobody is in the game yet.
public static class StartGameEndpoint
{
  [WolverinePost("/api/games")]
  public static async Task<Ok<StartGameResponse>> Post(IGameCreator creator, CancellationToken ct)
  {
    var id = await creator.CreateGameAsync(ct);
    return TypedResults.Ok(new StartGameResponse(id));
  }
}
