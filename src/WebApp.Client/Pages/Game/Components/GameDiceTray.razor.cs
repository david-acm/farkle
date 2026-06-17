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
  private async Task ToggleAsync(TrayDie die)
  {
    if (!GameState.IsMyTurn) return;

    var target = die.IsSelected ? DiceZone.Rolled : DiceZone.SetAside;
    await Mediator.Send(new GameState.SetDiceAside.Action(die, target));

    Logger.LogDebug("Tapped die {Index} -> {Target}", die.Index, target);
  }

  // A die finished its roll animation — clear the one-shot animate flag (via the handler) so
  // dice render statically on later re-renders. All dice finish ~together, so guard on "is
  // there anything left to consume" to dispatch once instead of once per die.
  private async Task ConsumeRollAnimationAsync()
  {
    if (!GameState.DiceInPlay.Any(d => d.IsAnimated)) return;
    await Mediator.Send(new GameState.ConsumeRollAnimation.Action());
  }
}
