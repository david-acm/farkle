using BlazorState;
using Farkle.Spa.Services;
using static Farkle.Spa.Components.DragabbleDice;

namespace Farkle.Spa.Features;

public partial class GameState
{
  public static class SetDiceAside
  {
    public record Action(DraggableDie Die) : IAction;
    
    public class Handler(IStore store) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();
      
      public override Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var die = State.DiceInPlay.First(d => d.Value == action.Die.Value);
        
        State.DiceSetAside.Add(die.Value);
        
        return Task.CompletedTask;
      }
    }
  }
}
