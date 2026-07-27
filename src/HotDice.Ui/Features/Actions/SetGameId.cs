using BlazorState;

namespace HotDice.Ui.Features;

public partial class GameState
{
  public static partial class SetGameId
  {
    public record Action(GameId GameId) : IAction;

    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override Task Handle(Action action, CancellationToken cancellationToken)
      {
        State.GameId = action.GameId;
        return Task.CompletedTask;
      }
    }
  }
}
