using BlazorState;

namespace HotDice.Ui.Features;

public partial class GameState
{
  public static class LeaveGame
  {
    public record Action : IAction;

    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        State.Initialize();
        return Task.CompletedTask;
      }
    }
  }
}
