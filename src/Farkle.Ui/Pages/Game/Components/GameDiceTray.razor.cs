using Blazor.Dice;
using BlazorState;
using Microsoft.AspNetCore.Components;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class GameDiceTray : GameStateComponent
{
  [Inject]
  public ILogger<GameDiceTray> Logger { get; set; } = null!;


  // A tap toggles the die between selected (SetAside) and unselected (Rolled) via the same
  // SetDiceAside action selection uses (#159): the choice is persisted and broadcast to
  // spectators. The presentational tray already ignores taps when ReadOnly; this guards too.
  private async Task ToggleAsync(DiceInfo die)
  {
    if (!GameState.IsMyTurn) return;

    var selected = !die.IsSelected;
    await Mediator.Send(new GameState.SetDiceAside.Action(die, selected));

    Logger.LogDebug("Tapped die {Index} -> selected={Selected}", die.Index, selected);
  }

  // A die finished its roll animation — clear the one-shot animate flag (via the handler) so
  // dice render statically on later re-renders. The tray reports which die finished; all dice
  // finish ~together, so we clear the whole board once (guarded) rather than once per die.
  private async Task ConsumeRollAnimationAsync(DiceInfo die)
  {
    if (!GameState.DiceInPlay.Any(d => d.IsAnimated)) return;
    await Mediator.Send(new GameState.ConsumeRollAnimation.Action());
  }
}
