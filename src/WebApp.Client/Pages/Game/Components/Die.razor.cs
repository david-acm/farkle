using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using WebApp.Client.Services;

namespace WebApp.Client.Pages.Game.Components;

public partial class Die
{
  private string          _id     = new(Guid.NewGuid().ToString().Where(c => !char.IsDigit(c)).ToArray());
  private DieValue       _number = DieValue.None;
  private (int, int, int) _rotation;
  private double          _scale = 1;
  
  [Parameter] public DieValue DieValue { get; set; } = null!;
  
  [Parameter] public int Size { get; set; } = 50;
  
  [Parameter] public string? Class { get; set; }
  
  // True only for a freshly rolled die: it plays the roll (spin) animation once.
  // Captured at init so a later model change can't retrigger the animation.
  [Parameter] public bool Animate { get; set; }

  private bool _animate;
  private bool _rotated;

  [Inject] public ILogger<Die> Logger { get; set; } = null!;

  [Inject] public IRotationCalculator RotationCalculator { get; set; } = null!;

  private double AngleFor(char a) => a switch
  {
    'x' => _rotation.Item1,
    'y' => _rotation.Item2,
    'z' => _rotation.Item3,
    _   => 0
  };

  protected override void OnInitialized() => _animate = Animate;

  protected override void OnParametersSet()
  {
    // A settled or moved die (not a fresh roll) renders its face immediately, with
    // no spin and nothing to animate (the first render already holds the final
    // rotation, so the CSS transition has no prior state to animate from). A rolling
    // die is left at its neutral orientation here and rotated after the first render
    // (OnAfterRender), which is what produces the visible roll spin.
    if (!_animate && (!_rotated || _number != DieValue)) RotateToValue();
  }

  protected override void OnAfterRender(bool firstRender)
  {
    if (firstRender && _animate) { RotateToValue(); StateHasChanged(); }
  }

  private void RotateToValue()
  {
    _number  = DieValue;
    _rotated = true;
    // randomSpin == _animate: only a freshly rolled die gets the random full-turn spins.
    var rotation = RotationCalculator.CalculateFor(_number, _animate);
    SetRotationTo(rotation);
    Logger.LogDebug("Rotating to: {x}, {y}, {z}", _rotation.Item1, _rotation.Item2, _rotation.Item3);
  }
  
  private void SetRotationTo((int, int, int) rotation) =>
    _rotation = rotation;
  
  private void MouseLeave(MouseEventArgs e)
  {
    (var x, var y, var z) = _rotation;
    SetRotationTo((x, y - 10, z - 10));
    Scale(1);
    StateHasChanged();
  }
  
  private void MouseEnter(MouseEventArgs e)
  {
    (var x, var y, var z) = _rotation;
    SetRotationTo((x, y + 10, z + 10));
    Scale(1.4);
    StateHasChanged();
  }
  
  private void Scale(double scale) =>
    _scale = scale;
}
