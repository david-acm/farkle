using Eventuous;
using Eventuous.Subscriptions.Context;
using Farkle.Domain.Feedback;

namespace Farkle.Application;

/// <summary>
/// #277 — projects <c>FeedbackSubmitted</c> into the <c>FeedbackView</c> read model for triage.
/// Unlike <see cref="GameViewProjector"/> there's no aggregate to fold through: feedback is a flat
/// fact stream, so each event maps directly to one row (keyed by its $all position for idempotency).
/// Subscribes to the "Feedback-" stream prefix; registered as its own durable subscription in
/// Farkle.Infrastructure (ReadModelServiceExtensions).
/// </summary>
public sealed class FeedbackViewProjector : Eventuous.Subscriptions.EventHandler
{
  private readonly IFeedbackViewStore _store;

  public FeedbackViewProjector(IFeedbackViewStore store)
  {
    _store = store;
    On<FeedbackEvents.V1.FeedbackSubmitted>(Project);
  }

  private async ValueTask Project(MessageConsumeContext<FeedbackEvents.V1.FeedbackSubmitted> ctx)
  {
    var e = ctx.Message;
    await _store.SaveAsync(
      new FeedbackViewRecord(
        (long)ctx.GlobalPosition,
        SessionIdFrom(ctx.Stream),
        e.GameId, e.PlayerId, e.Stage, e.Message, e.Sentiment, e.Route, e.At),
      ctx.CancellationToken);
  }

  // Stream is "Feedback-{sessionId}". The session id itself may contain dashes, so split on the
  // first dash only and keep the remainder.
  private static string SessionIdFrom(StreamName stream)
  {
    var name = stream.ToString();
    var dash = name.IndexOf('-');
    return dash >= 0 ? name[(dash + 1)..] : name;
  }
}
