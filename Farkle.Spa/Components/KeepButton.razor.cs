using Microsoft.AspNetCore.Components;

namespace Farkle.Spa.Components;

public partial class KeepButton
{
  // TODO: replace by value object
  [Parameter] public EventCallback OnKeep { get; set; }
  private async Task KeepAsync()
  {
    await OnKeep.InvokeAsync();
  }
}
