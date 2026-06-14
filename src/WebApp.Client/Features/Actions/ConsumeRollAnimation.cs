using BlazorState;
using WebApp.Client.Pages.Game.Components;

namespace WebApp.Client.Features;

public partial class GameState
{
  // Dispatched by the dice view once a die has finished its roll (spin) animation.
  // Clears the one-shot Animate flag on every die so that, when the drop container
  // recreates a die's component on a later re-render (e.g. dragging a die to another
  // zone), it renders its face statically instead of replaying the spin (#139).
  //
  // The mutation lives here, in a handler, rather than in the component — the view
  // never mutates DiceInPlay directly (same rule as SetDiceAside).
  public static class ConsumeRollAnimation
  {
    public record Action : IAction;

    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        foreach (DraggableDie die in State.DiceInPlay)
          die.Animate = false;
        return Task.CompletedTask;
      }
    }
  }
}
