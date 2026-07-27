namespace HotDice.Ui.Telemetry;

/// <summary>
/// #221 — marks a BlazorState action that was triggered by a SignalR broadcast and carries the
/// originating command's trace id (the App Insights <c>operation_Id</c>, propagated server → hub →
/// client). <see cref="UiTelemetryBehavior{TRequest,TResponse}"/> stamps it onto the action's
/// <c>UI.*</c> event as a <c>causedByOperationId</c> property.
///
/// Link-by-property, not operation-id-overwrite: the client event stays in <b>this</b> client's own
/// page-session <c>operation_Id</c> (so the per-client session view is preserved) and merely
/// <i>links</i> back to the command via a custom dimension. A broadcast fans out 1→N, so N clients
/// each keep their own session and all join back to the one command by this property in KQL —
/// rather than being force-nested into a single transaction tree.
/// </summary>
public interface ICausedByBroadcast
{
  string? CausedByOperationId { get; }
}
