using BlazorState;

namespace WebApp.Client.Features;

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
        // STUB (commit 1): real implementation in commit 2 seeds State.GameId.
        _ = State;
        return Task.CompletedTask;
      }
    }
  }
}
