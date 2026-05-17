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
        var response = await service.JoinPlayerAsync(State.GameId, action.PlayerName);

        State.PlayerId        = new PlayerId(response.Id);
        State.PlayerName      = action.PlayerName;
        State.CurrentPlayerId = response.CurrentPlayerId;
        // Seed scoreboard at zero; PassTurn responses replace it with authoritative data.
        State.Scoreboard = [new PlayerStanding(response.Id, action.PlayerName.Value, 0)];
      }
    }
  }
}
