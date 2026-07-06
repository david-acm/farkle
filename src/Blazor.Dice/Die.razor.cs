using System.Threading;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazor.Dice;

public partial class Die : IAsyncDisposable
{
  private readonly string     _id     = new(Guid.NewGuid().ToString().Where(c => !char.IsDigit(c)).ToArray());
  private DieValue            _number = DieValue.None;
  private DieRotation         _rotation;
  private double              _scale = 1;
  private Timer?              _consumeTimer;
  private IJSObjectReference? _spinModule;
  private bool                _disposed;

  // Static asset of the Blazor.Dice RCL (served at _content/<assembly>/…).
  private const string SpinModulePath = "./_content/Blazor.Dice/die-spin.js";

  /// <summary>The die face to show (None or One–Six).</summary>
  [Parameter, EditorRequired] public DieValue DieValue { get; set; } = null!;

  /// <summary>Die size in pixels (drives the <c>--die-size</c> custom property).</summary>
  [Parameter] public int Size { get; set; } = 50;
  
  [Parameter] public string? Class { get; set; }

  // #182 — a selected (tapped) die: yellow face + dark pips, applied by Die.razor.css off
  // the `.selected` class. The ×1.2 scale is applied by the tray wrapper (DiceTray.razor.css).
  [Parameter] public bool Selected { get; set; }

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

  [Inject] public IJSRuntime JS { get; set; } = null!;

  private double AngleFor(char a) => a switch
  {
    'x' => _rotation.X,
    'y' => _rotation.Y,
    'z' => _rotation.Z,
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
    // A freshly rolled die rendered at its neutral orientation. Rotate to the face only after
    // the browser has PAINTED that neutral state — the CSS transition then has a "from" state
    // and animates the roll. Doing it inline (synchronous StateHasChanged) renders the final
    // rotation in the same frame, so no transition fires and the roll looks static. Gated on
    // _animate, so settled or moved dice (which set their face synchronously in OnParametersSet)
    // never spin.
    if (!firstRender || !_animate || _rotated) return;

    // 1) Rotate to the face once a real frame has painted (see SpinAfterPaintAsync). A prior
    //    Timer(0) fired on the next microtask — usually BEFORE the neutral paint — so the
    //    transition intermittently didn't fire and the dice snapped to their faces with no
    //    spin (worst on a turn's first roll). requestAnimationFrame waits for an actual paint.
    _ = SpinAfterPaintAsync();

    // 2) After the transition finishes, ask the owner to clear the model's Animate flag
    //    (a one-shot), so a later recreation of this component renders statically. Doing
    //    this only once the spin is done means the clear-driven re-render can't cut it.
    _consumeTimer = new Timer(_ => InvokeAsync(() =>
      OnAnimated.HasDelegate ? OnAnimated.InvokeAsync() : Task.CompletedTask),
      null, AnimationDurationMs, Timeout.Infinite);
  }

  // Wait for the neutral orientation to actually paint, then rotate to the face so the CSS
  // transition animates. The paint-wait is via requestAnimationFrame in JS (die-spin.js). If
  // JS isn't available (component tests, static prerender, or teardown mid-flight) we fall back
  // to a short delay so the die still settles on its face — the visual spin just isn't observed.
  private async Task SpinAfterPaintAsync()
  {
    try
    {
      _spinModule ??= await JS.InvokeAsync<IJSObjectReference>("import", SpinModulePath);
      if (_spinModule is not null)
        await _spinModule.InvokeVoidAsync("afterNextPaint");
    }
    catch (Exception ex) when (ex is JSException or JSDisconnectedException or OperationCanceledException or ObjectDisposedException)
    {
      // JS unavailable / circuit gone / disposed mid-flight — settle on the face without the
      // paint-wait (the spin just isn't observed in that rare case).
      Logger.LogDebug(ex, "Die spin paint-wait unavailable; settling without requestAnimationFrame");
    }

    if (_disposed || _rotated) return;
    RotateToValue();
    StateHasChanged();
  }

  // Blazor disposes the component when it is removed (e.g. recreated for a zone move).
  // Dispose the one-shot timer + the JS module so they don't leak — important because a die's
  // component is recreated whenever it moves between the rolled/set-aside zones.
  public async ValueTask DisposeAsync()
  {
    _disposed = true;
    _consumeTimer?.Dispose();
    if (_spinModule is not null)
    {
      try { await _spinModule.DisposeAsync(); }
      catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or OperationCanceledException)
      {
        // Circuit/browser gone during teardown — nothing to release.
      }
    }
  }

  private void RotateToValue()
  {
    _number  = DieValue;
    _rotated = true;
    // randomSpin == _animate: only a freshly rolled die gets the random full-turn spins.
    var rotation = RotationCalculator.CalculateFor(_number, _animate);
    SetRotationTo(rotation);
    Logger.LogDebug("Rotating to: {x}, {y}, {z}", _rotation.X, _rotation.Y, _rotation.Z);
  }

  private void SetRotationTo(DieRotation rotation) =>
    _rotation = rotation;

  private void MouseLeave(MouseEventArgs e)
  {
    var (x, y, z) = _rotation;
    SetRotationTo(new DieRotation(x, y - 10, z - 10));
    Scale(1);
    StateHasChanged();
  }

  private void MouseEnter(MouseEventArgs e)
  {
    var (x, y, z) = _rotation;
    SetRotationTo(new DieRotation(x, y + 10, z + 10));
    Scale(1.4);
    StateHasChanged();
  }
  
  private void Scale(double scale) =>
    _scale = scale;
}
