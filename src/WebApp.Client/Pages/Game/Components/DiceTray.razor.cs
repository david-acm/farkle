using BlazorState;
using Microsoft.AspNetCore.Components;
using WebApp.Client.Features;

namespace WebApp.Client.Pages.Game.Components;

public partial class DiceTray : BlazorStateComponent
{
  [Inject]
  public ILogger<DiceTray> Logger { get; set; } = null!;

  private GameState GameState => GetState<GameState>();

  // Tap toggles the die between selected (SetAside) and unselected (Rolled). This drives
  // the same SetDiceAside action the drag used to (#159): it flips the identifier locally
  // and syncs to the server, so the selection is persisted + broadcast. Off-turn players
  // see the tray read-only, so their taps are ignored here (the server would reject them
  // anyway, but this avoids flipping their local view).
  private async Task ToggleAsync(DraggableDie die)
  {
    if (!GameState.IsMyTurn) return;

    var target = die.Identifier == "SetAside" ? "Rolled" : "SetAside";
    await Mediator.Send(new GameState.SetDiceAside.Action(die, target));

    Logger.LogDebug("Tapped die {Index} -> {Target}", die.Index, target);
  }

  // A die finished its roll animation — clear the one-shot Animate flag (via the handler)
  // so dice render statically on later re-renders. All dice finish ~together, so guard on
  // "is there anything left to consume" to dispatch once instead of once per die.
  private async Task ConsumeRollAnimationAsync()
  {
    if (!GameState.DiceInPlay.Any(d => d.Animate)) return;
    await Mediator.Send(new GameState.ConsumeRollAnimation.Action());
  }
}
