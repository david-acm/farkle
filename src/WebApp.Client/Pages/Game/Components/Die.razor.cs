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

  // Raised once the roll animation has *finished*, so the owner can dispatch an
  // action that clears the model's Animate flag — making the spin a one-shot. This is
  // what stops a die from re-spinning when the drop container recreates its component
  // on a later re-render (e.g. when a sibling die is moved between zones). Fired after
  // the transition completes so clearing the flag (and the resulting re-render) cannot
  // cut the animation short.
  [Parameter] public EventCallback OnAnimated { get; set; }

  // Roll animation length; matches the `.die { transition: 1.4s }` in Die.razor.css.
  // Exposed as a parameter so component tests can shrink the consume delay.
  [Parameter] public int AnimationDurationMs { get; set; } = 1400;

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
    // Already showing this value → do nothing, so an unrelated re-render (e.g. a
    // sibling die being moved) never re-triggers a spin.
    if (_rotated && _number == DieValue) return;

    // First paint of a freshly rolled die: stay at the neutral orientation and let
    // OnAfterRender rotate to the face, so the CSS transition animates the roll.
    if (!_rotated && _animate) return;

    // Settled/moved die's first paint (face shown instantly, no prior state to
    // animate from → no spin), OR a new value landing on an existing die (a
    // re-roll on a reused instance) → rotate now; the transition animates it,
    // and RotateToValue only adds spin when this die animates.
    RotateToValue();
  }

  protected override void OnAfterRender(bool firstRender)
  {
    // A freshly rolled die rendered at its neutral orientation. Rotate to the face on
    // a *later* tick (Timer) so the browser paints the neutral state first — the CSS
    // transition then has a "from" state and animates the roll. Doing this inline
    // (synchronous StateHasChanged) renders the final rotation in the same frame, so
    // no transition fires and the roll looks static. Gated on _animate, so settled or
    // moved dice (which set their face synchronously in OnParametersSet) never spin.
    if (!firstRender || !_animate || _rotated) return;

    // 1) After the neutral paint, rotate to the face → the CSS transition animates the roll.
    _ = new Timer(_ => InvokeAsync(() => { RotateToValue(); StateHasChanged(); }), null, 0, -1);

    // 2) After the transition finishes, ask the owner to clear the model's Animate flag
    //    (a one-shot), so a later recreation of this component renders statically. Doing
    //    this only once the spin is done means the clear-driven re-render can't cut it.
    _ = new Timer(_ => InvokeAsync(() =>
      OnAnimated.HasDelegate ? OnAnimated.InvokeAsync() : Task.CompletedTask),
      null, AnimationDurationMs, -1);
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
