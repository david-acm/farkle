using BlazorState;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class KeepDice
  {
    public record Action() : IAction;

    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.KeepDiceAsync(State.GameId, State.PlayerId, State.DiceSetAside);
        State.DiceInPlay.RemoveAll(d => d.Identifier == "SetAside");

        State.TurnScore = new(response.TurnScore);
      }
    }
  }
}
