using BlazorState;
using WebApp.Client.Pages.Game.Components;

namespace WebApp.Client.Features;

public partial class GameState
{
  public static class SetDiceAside
  {
    public record Action(DraggableDie Die) : IAction;
    
    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();
      
      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var die = State.DiceInPlay.First(d => d.Value == action.Die.Value);
        die.Identifier = action.Die.Identifier;

        if (action.Die.Identifier == "SetAside")
          State.DiceSetAside.Add(die.Value);
        else
          State.DiceSetAside.Remove(die.Value);

        return Task.CompletedTask;
      }
    }
  }
}
