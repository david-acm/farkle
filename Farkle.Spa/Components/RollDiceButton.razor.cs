using Microsoft.AspNetCore.Components;

namespace Farkle.Spa.Components;

public partial class RollDiceButton
{
  [Parameter] public EventCallback OnRoll { get; set; }
  private async Task RollAsync()
  {
    await OnRoll.InvokeAsync();
  }
}
