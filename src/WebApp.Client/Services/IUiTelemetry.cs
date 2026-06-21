namespace WebApp.Client.Services;

/// <summary>
/// #225 — emits client-side "user intent" custom events to Application Insights so the UI
/// funnel (which buttons users press, which actions fail before reaching the API, local-only
/// actions like set-aside that never hit the server) is queryable alongside the server-side
/// domain events. Wraps the <c>window.farkleTelemetry</c> JS helper (see
/// <c>WebApp/Components/App.razor</c>) so handlers never touch JS interop directly.
///
/// Registered once in <see cref="ClientServiceExtensions.RegisterClientServices"/>, which both
/// hosts call, so the same implementation serves the InteractiveServer and InteractiveWebAssembly
/// render modes. Telemetry is best-effort: failures (including no App Insights configured locally)
/// are swallowed.
/// </summary>
public interface IUiTelemetry
{
  /// <summary>
  /// Tracks a UI intent custom event. <paramref name="name"/> is the event name (e.g.
  /// <c>"UI.RollDice"</c>); <paramref name="properties"/> are queryable string dimensions.
  /// </summary>
  Task TrackIntentAsync(string name, IReadOnlyDictionary<string, string?> properties);

  /// <summary>
  /// #244 / #250 — starts a fresh App Insights operation for a server-bound command and records the
  /// command (<paramref name="commandName"/>, e.g. <c>"RollDice"</c>) as a real UI-action span, so
  /// the command's request → events → broadcast nest UNDER it in the transaction tree (one
  /// transaction per command). No-op when App Insights isn't loaded.
  /// </summary>
  Task StartOperationAsync(string commandName);
}
