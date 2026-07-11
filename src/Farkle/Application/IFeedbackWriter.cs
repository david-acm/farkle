using Farkle.Domain.Feedback;

namespace Farkle.Application;

// #277 — thin append-only writer for user feedback. No aggregate / command handler / validator:
// feedback has no cross-event invariants, so we append the FeedbackSubmitted fact straight to its
// own "feedback-{sessionId}" Marten stream with no optimistic-concurrency check (ADR 0004). Marten
// emits its own append span, correlated to the originating request via the ambient trace context.
internal interface IFeedbackWriter
{
  Task AppendAsync(string sessionId, FeedbackEvents.V1.FeedbackSubmitted @event, CancellationToken ct);
}
