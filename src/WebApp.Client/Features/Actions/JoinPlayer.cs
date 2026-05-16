using BlazorState;
using Farkle.Spa.Services;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class JoinPlayer
  {
    // TODO: refactor playerId to player
    public record Action(PlayerId PlayerId, PlayerName PlayerName) : IAction;
    
    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // TODO: refactor signature
        await service.JoinPlayerAsync(State.GameId, action.PlayerId, action.PlayerName);

        State.PlayerId        = action.PlayerId;
        State.PlayerName      = action.PlayerName;
        // In a single-player session the joining player is always first in turn.
        // PassTurn responses will correct this for multi-player games.
        if (State.CurrentPlayerId == 0)
          State.CurrentPlayerId = action.PlayerId.Value;
        // Seed scoreboard until a PassTurn response provides authoritative data.
        if (State.Scoreboard.Count == 0)
          State.Scoreboard = [new PlayerStanding(action.PlayerId.Value, action.PlayerName.Value, 0)];
      }
    }
  }
}
