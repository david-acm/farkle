using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class JoinPlayer
  {
    public record Action(PlayerName PlayerName) : IAction;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var assignedId = await service.JoinPlayerAsync(State.GameId, action.PlayerName);

        State.PlayerId        = new PlayerId(assignedId);
        State.PlayerName      = action.PlayerName;
        // In a single-player session the joining player is always first in turn.
        // PassTurn responses will correct this for multi-player games.
        if (State.CurrentPlayerId == 0)
          State.CurrentPlayerId = assignedId;
        // Seed scoreboard at zero; PassTurn responses replace it with authoritative data.
        State.Scoreboard = [new PlayerStanding(assignedId, action.PlayerName.Value, 0)];
      }
    }
  }
}
