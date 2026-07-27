using BlazorState;
using MediatR;
using Farkle.Ui.Features;
using Farkle.Ui.Services;

namespace Farkle.Ui.Telemetry;

/// <summary>
/// #225 — a MediatR pipeline behavior that emits a UI intent custom event for every dispatched
/// BlazorState <see cref="IAction"/>, so telemetry is automatic for current <i>and</i> future
/// action handlers without per-handler wiring. BlazorState is built on MediatR, so registering
/// this open-generic behavior wraps every action dispatch.
///
/// Tracking is opt-out: all actions are tracked unless marked <see cref="IInternalAction"/>.
/// Non-action MediatR requests (BlazorState's own infrastructure messages) are passed straight
/// through. The event fires in a <c>finally</c> so a failing handler is still recorded (with
/// <c>Failed=true</c>) and a telemetry hiccup never alters control flow.
/// </summary>
public sealed class UiTelemetryBehavior<TRequest, TResponse>(IUiTelemetry telemetry, IStore store)
  : IPipelineBehavior<TRequest, TResponse>
  where TRequest : notnull
{
  public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
  {
    if (request is not IAction || request is IInternalAction)
      return await next();

    var intent = IntentName(request);

    // #244 / #250 — a server-bound command starts its own App Insights operation rooted at a real
    // UI-action span (named after the command), so the command's request → events → broadcast nest
    // UNDER it in one transaction. Done before next() so the handler's fetch adopts the new context.
    if (request is IServerCommandIntent)
      await telemetry.StartOperationAsync(intent);

    var failed = false;
    try
    {
      return await next();
    }
    catch
    {
      failed = true;
      throw;
    }
    finally
    {
      await TrackAsync(request, intent, failed);
    }
  }

  // Game actions are nested records literally named "Action" (e.g. GameState.RollDice.Action), so
  // the intent is the *declaring* type's name — "RollDice", "PassTurn", "RemoteTurnChanged". An
  // action not following that convention falls back to its own type name.
  private static string IntentName(TRequest request)
  {
    var type = request.GetType();
    return type.Name == "Action" ? type.DeclaringType?.Name ?? type.Name : type.Name;
  }

  private async Task TrackAsync(TRequest request, string intent, bool failed)
  {
    // Post-action state: ids reflect the action's effect (e.g. GameId after CreateGame), which is
    // what we want for correlation.
    var state = store.GetState<GameState>();
    var props = new Dictionary<string, string?>
    {
      ["GameId"]   = state.GameId.Value.ToString(),
      ["PlayerId"] = state.PlayerId.Value.ToString(),
      // #244 — turn entity key, so telemetry can be grouped/joined per turn across players.
      ["turnId"]   = state.TurnNumber.ToString(),
      ["Failed"]   = failed ? "true" : "false",
    };

    // #221 — a broadcast-triggered action links back to the originating command's trace id, so a
    // KQL join reconstructs "player A's command → player B's screen" without overwriting this
    // client's own session operation_Id.
    if (request is ICausedByBroadcast { CausedByOperationId: { Length: > 0 } causedBy })
      props["causedByOperationId"] = causedBy;

    await telemetry.TrackIntentAsync($"UI.{intent}", props);
  }
}
