using Microsoft.AspNetCore.Components;

namespace Farkle.Spa.Components;

public partial class TurnScore : ComponentBase
{
  [Parameter]
  public int Value { get; set; } = 0;
}

