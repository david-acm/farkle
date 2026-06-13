using BlazorState;
using WebApp.Client.Pages.Game.Components;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class SetDiceAside
  {
    // Carries the target zone explicitly so the UI never mutates the dropped
    // die outside an action handler. Backwards-compatible single-arg form is
    // kept for callers (and tests) that already encode the identifier on Die.
    public record Action(DraggableDie Die, string Identifier) : IAction
    {
      public Action(DraggableDie die) : this(die, die.Identifier) { }
    }

    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();

      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        // DiceInPlay is the single source of truth; the set-aside payload is
        // derived from it. Sync the dropped die's zone identifier, and clear its
        // Animate flag — a move is not a roll, so the die must not spin (#139).
        var die = State.DiceInPlay.First(d => d.Index == action.Die.Index);
        die.Identifier = action.Identifier;
        die.Animate    = false;
        return Task.CompletedTask;
      }
    }
  }
}
