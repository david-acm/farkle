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

      public override async Task Handle(Action action, CancellationToken cancellationToken)
      {
        var id = await service.CreateGameAsync();
        State.GameId = new(id);
      }
    }
  }
}
