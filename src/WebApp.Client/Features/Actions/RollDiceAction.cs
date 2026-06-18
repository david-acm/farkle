using BlazorState;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;

namespace WebApp.Client.Features;

public partial class GameState
{
  public class RollDice
  {
    public record Action() : IAction;
    
    public class Handler(IStore store, IGameService service, ILogger<RollDice> logger) 
      : ActionHandler<Action>(store)
    {
      private WebApp.Client.Features.GameState State => Store.GetState<WebApp.Client.Features.GameState>();
      
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
          .Select((d, i) => DiceInfo.Unselected(i, DieValue.FromValue(d)))
          .ToList();
      }
    }
  }
}
