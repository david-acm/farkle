using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WebApp.Client.Services;

namespace WebApp.Client.Layout;

// #277 — global floating feedback widget (rendered in MainLayout, so it's on every page). Clicking
// it opens a small text + 👍/👎 form that posts to /api/feedback. A client-side rage-click burst
// (detected by the window.farkleRageClick JS helper) pulses the icon, surfaces a hint, and records
// a UI.RageClick custom event — no backend call. This is the repo's first [JSInvokable] callback.
public partial class FeedbackWidget : IAsyncDisposable
{
  private const string Up   = "Up";
  private const string Down = "Down";
  private const int    RageClickThreshold = 4;
  private const int    RageWindowMs       = 1000;

  [Inject] public IFeedbackService         Feedback  { get; set; } = null!;
  [Inject] public IUiTelemetry             Telemetry { get; set; } = null!;
  [Inject] public IJSRuntime               Js        { get; set; } = null!;
  [Inject] public NavigationManager        Nav       { get; set; } = null!;
  [Inject] public ILogger<FeedbackWidget>  Logger    { get; set; } = null!;

  private bool   _open;
  private bool   _pulsing;
  private bool   _hint;
  private bool   _sending;
  private bool   _sent;
  private string _message   = string.Empty;
  private string _sentiment = Up;

  private DotNetObjectReference<FeedbackWidget>? _selfRef;

  protected override async Task OnAfterRenderAsync(bool firstRender)
  {
    if (!firstRender) return;
    _selfRef = DotNetObjectReference.Create(this);
    try
    {
      await Js.InvokeVoidAsync("farkleRageClick.start", _selfRef, RageClickThreshold, RageWindowMs);
    }
    catch (JSException ex)            { Logger.LogDebug(ex, "Rage-click detection unavailable"); }
    catch (InvalidOperationException) { /* prerender — best-effort */ }
  }

  // Invoked from JS once per debounced rapid-click burst. Records a UI.RageClick custom event
  // (for the funnel/abandonment analysis) and nudges the user toward the feedback form.
  [JSInvokable]
  public async Task OnRageBurst(int clickCount)
  {
    await Telemetry.TrackIntentAsync("UI.RageClick", new Dictionary<string, string?>
    {
      ["clickCount"] = clickCount.ToString(),
      ["route"]      = CurrentRoute(),
      ["GameId"]     = CurrentGameId()?.ToString(),
    });

    if (_open) return; // already engaged — don't nag
    _pulsing = true;
    _hint    = true;
    await InvokeAsync(StateHasChanged);
  }

  private void Toggle()
  {
    _open    = !_open;
    _pulsing = false;
    _hint    = false;
    if (_open) _sent = false;
  }

  private void Close()
  {
    _open    = false;
    _pulsing = false;
    _hint    = false;
  }

  private async Task SubmitAsync()
  {
    if (_sending || string.IsNullOrWhiteSpace(_message)) return;
    _sending = true;
    try
    {
      await Feedback.SubmitFeedbackAsync(_message.Trim(), _sentiment, CurrentGameId(), CurrentRoute());
      _sent    = true;
      _message = string.Empty;
    }
    catch (Exception ex)
    {
      Logger.LogWarning(ex, "Submitting feedback failed");
    }
    finally
    {
      _sending = false;
    }
  }

  private string CurrentRoute() => new Uri(Nav.Uri).AbsolutePath;

  // /games/{id} → id (best-effort triage context); null elsewhere.
  private int? CurrentGameId()
  {
    var segments = new Uri(Nav.Uri).AbsolutePath.Trim('/').Split('/');
    return segments.Length >= 2 && segments[0] == "games" && int.TryParse(segments[1], out var id)
      ? id
      : null;
  }

  public async ValueTask DisposeAsync()
  {
    try { await Js.InvokeVoidAsync("farkleRageClick.stop"); }
    catch (JSException)              { /* circuit gone — best-effort */ }
    catch (InvalidOperationException) { /* prerender — best-effort */ }
    _selfRef?.Dispose();
  }
}
