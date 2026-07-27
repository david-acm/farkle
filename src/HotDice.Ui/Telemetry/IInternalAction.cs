using BlazorState;

namespace HotDice.Ui.Telemetry;

/// <summary>
/// #225 — opt-out marker for the <see cref="UiTelemetryBehavior{TRequest,TResponse}"/>.
/// UI intent telemetry is <b>on by default</b> for every dispatched <see cref="IAction"/>
/// (user-initiated <i>and</i> SignalR-driven remote actions), so future actions are captured
/// without touching anything. Mark an action with this interface only to exclude it — e.g. a
/// high-churn, low-signal housekeeping action that would otherwise be pure noise.
/// </summary>
public interface IInternalAction;
