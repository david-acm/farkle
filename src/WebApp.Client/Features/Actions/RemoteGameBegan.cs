using BlazorState;
using static Farkle.Contracts.HttpResponses;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class RemoteGameBegan
  {
    public record Action(LobbyStateResponse Payload) : IAction;

    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var p = action.Payload;
        State.GameStage       = p.Stage;
        State.CurrentPlayerId = p.CurrentPlayerId;
        State.HostPlayerId    = p.HostPlayerId;
        State.Scoreboard      = p.Players
          .Select(lp => new PlayerStanding(lp.PlayerId, lp.Name, 0))
          .ToList();
        return Task.CompletedTask;
      }
    }
  }
}
