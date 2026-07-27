using BlazorState;
using Farkle.Ui.Telemetry;
using Farkle.Ui.Pages.Game.Components;
using Farkle.Ui.Services;

namespace Farkle.Ui.Features;

public partial class GameState
{
  public class RollDice
  {
    public record Action() : IAction, IServerCommandIntent;
    
    public class Handler(IStore store, IGameService service, ILogger<RollDice> logger) 
      : ActionHandler<Action>(store)
    {
      private Farkle.Ui.Features.GameState State => Store.GetState<Farkle.Ui.Features.GameState>();
      
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

        // #286 — a roll moves the turn into the keeping stage and counts as having acted
        // (so Pass becomes valid); a second roll is gated until the player keeps.
        State.PlayStage        = Farkle.SharedKernel.Turns.GameStage.Keeping;
        State.HasActedThisTurn = true;
      }
    }
  }
}
