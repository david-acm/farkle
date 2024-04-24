using BlazorState;
using Farkle.Spa.Services;

namespace Farkle.Spa.Features;

public partial class GameState
{
  public class KeepDice
  {
    public record Action() : IAction;
    
    public class Handler(IStore store, IGameService service) : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.KeepDiceAsync(State.GameId, State.PlayerId, State.DiceSetAside);
        State.DiceInPlay = State.DiceInPlay.Where(d => d.Identifier == "Rolled").ToList();
        State.DiceSetAside.Clear();
        
        State.TurnScore = new(response.TurnScore);
      }
    }
  }
}
