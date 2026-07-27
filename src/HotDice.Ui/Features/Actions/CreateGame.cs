using BlazorState;
using HotDice.Ui.Telemetry;
using HotDice.Ui.Services;

namespace HotDice.Ui.Features;

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
