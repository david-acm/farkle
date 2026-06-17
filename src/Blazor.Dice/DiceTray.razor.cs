using Microsoft.AspNetCore.Components;

namespace Blazor.Dice;

// Presentational, callback-driven dice tray (#196). No state/BlazorState coupling: the owner
// passes the dice and handles OnToggle (a die was tapped) and OnDieAnimated (a roll spin
// finished). Read-only consumers (spectators) pass ReadOnly so taps are ignored.
public partial class DiceTray
{
  [Parameter, EditorRequired] public IReadOnlyList<TrayDie> Dice { get; set; } = [];

  [Parameter] public bool ReadOnly { get; set; }

  // Raised with the tapped die when the tray is interactive.
  [Parameter] public EventCallback<TrayDie> OnToggle { get; set; }

  // Raised once a die's roll animation finishes (forwarded from Die.OnAnimated).
  [Parameter] public EventCallback OnDieAnimated { get; set; }

  private Task OnTapAsync(TrayDie die) =>
    ReadOnly ? Task.CompletedTask : OnToggle.InvokeAsync(die);
}
