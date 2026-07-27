using Microsoft.JSInterop;

namespace Farkle.Ui.Services;

/// <summary>
/// <see cref="IUiTelemetry"/> backed by the <c>window.farkleTelemetry</c> helper
/// (see <c>WebApp/Components/App.razor</c>), which forwards to the App Insights JS SDK.
/// Failures are swallowed — telemetry must never break the UI:
/// <list type="bullet">
///   <item><see cref="JSException"/> — the JS helper threw (e.g. App Insights not loaded).</item>
///   <item><see cref="InvalidOperationException"/> — JS interop unavailable (e.g. during
///   server-side prerender, before the circuit is live).</item>
/// </list>
/// </summary>
public sealed class UiTelemetry(IJSRuntime js) : IUiTelemetry
{
  public async Task TrackIntentAsync(string name, IReadOnlyDictionary<string, string?> properties)
  {
    try
    {
      await js.InvokeVoidAsync("farkleTelemetry.trackEvent", name, properties);
    }
    catch (JSException)
    {
      // App Insights JS not loaded (no connection string) or the helper threw — ignore.
    }
    catch (InvalidOperationException)
    {
      // JS interop not available yet (prerender) — ignore.
    }
  }

  public async Task StartOperationAsync(string commandName)
  {
    try
    {
      await js.InvokeVoidAsync("farkleTelemetry.startCommand", commandName);
    }
    catch (JSException) { /* App Insights not loaded — best-effort. */ }
    catch (InvalidOperationException) { /* prerender — best-effort. */ }
  }
}
