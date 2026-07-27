using BlazorState;

namespace Farkle.Ui.Telemetry;

/// <summary>
/// #244 — marks a user action that issues a <b>server command</b> (an HTTP round-trip that produces
/// events + a broadcast). <see cref="UiTelemetryBehavior{TRequest,TResponse}"/> starts a fresh App
/// Insights operation before dispatching it, so each command → its server request → events →
/// broadcast becomes <b>one transaction per command</b> instead of everything sharing the page's
/// operation_Id.
///
/// Scope it to server-bound user intents only — NOT local/internal actions (which would fragment
/// the trace) and NOT broadcast-reaction actions (those stay in this client's session and link back
/// via <see cref="ICausedByBroadcast"/>, #221). A turn is reconstructed from these per-command
/// operations via the <c>turnId</c> dimension (#246), not by one operation per turn.
/// </summary>
public interface IServerCommandIntent : IAction;
