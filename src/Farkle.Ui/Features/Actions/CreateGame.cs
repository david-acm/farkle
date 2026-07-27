using BlazorState;
using Farkle.Ui.Telemetry;
using Farkle.Ui.Services;

namespace Farkle.Ui.Features;

public partial class GameState
{
  public static partial class CreateGame
  {
    public record Action() : IAction, IServerCommandIntent;

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
