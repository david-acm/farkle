using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static partial class CreateGame
  {
    public record Action() : IAction;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();

      public override Task Handle(Action action, CancellationToken cancellationToken)
      {
        // STUB (commit 1): real implementation in commit 2 creates the game via the API.
        _ = service;
        _ = State;
        return Task.CompletedTask;
      }
    }
  }
}
