using Microsoft.AspNetCore.Components;

namespace Blazor.Dice;

// Presentational, callback-driven dice tray (#196). No state/BlazorState coupling: the owner
// passes the dice and handles OnToggle (a die was tapped) and OnDieAnimated (a roll spin
// finished). Read-only consumers (spectators) pass ReadOnly so taps are ignored.
public partial class DiceTray
{
  [Parameter, EditorRequired] public IReadOnlyList<DiceInfo> Dice { get; set; } = [];

  [Parameter] public bool ReadOnly { get; set; }

  // Raised with the tapped die when the tray is interactive.
  [Parameter] public EventCallback<DiceInfo> OnToggle { get; set; }

  // Raised with the die whose roll animation just finished (bridged from Die.OnAnimated),
  // so the owner can clear that die's one-shot animate flag (e.g. die.DisableAnimation()).
  [Parameter] public EventCallback<DiceInfo> OnDieAnimated { get; set; }

  private Task OnTapAsync(DiceInfo die) =>
    ReadOnly ? Task.CompletedTask : OnToggle.InvokeAsync(die);

  private Task OnDieAnimatedAsync(DiceInfo die) => OnDieAnimated.InvokeAsync(die);
}
