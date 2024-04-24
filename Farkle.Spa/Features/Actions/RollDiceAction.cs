using BlazorState;
using Farkle.Spa.Components;
using Farkle.Spa.Services;
using static Farkle.Spa.Components.DragabbleDice;

namespace Farkle.Spa.Features;

public partial class GameState
{
  public class RollDice
  {
    public record Action() : IAction;
    
    public class Handler(IStore store, IGameService service, ILogger<RollDice> logger) 
      : ActionHandler<Action>(store)
    {
      private GameState State => Store.GetState<GameState>();
      
      public override async Task Handle(Action action, CancellationToken aCancellationToken)
      {
        var response = await service.RollDiceAsync(State.GameId, State.PlayerId);
        
        if (!response.IsSuccess)
        {
          State.Error         = true;
          State.ErrorMessage = string.Join(", ", response.Errors.ToList());
          return;
        }
        
        logger.LogDebug("Set aside dice being updated in game from roll: {setAsideDice}",
          response.Value.Select(d => d.Value));
        
        State.DiceInPlay = response.Value
          .Select((d, i) => new DraggableDie(i, DiceValue.FromValue(d), "Rolled"))
          .ToList();
      }
    }
  }
}
