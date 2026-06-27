using Farkle.Domain.Feedback;

namespace Farkle.Application;

// #277 — thin append-only writer for user feedback. No aggregate / command service / validator:
// feedback has no cross-event invariants, so we append the FeedbackSubmitted fact straight to its
// own "Feedback-{sessionId}" stream with no optimistic-concurrency check. The DI-registered
// IEventStore is Eventuous' TracedEventStore decorator, so the append still gets a producer span
// + W3C trace-context metadata (correlated back to the originating request) for free.
internal interface IFeedbackWriter
{
  Task AppendAsync(string sessionId, FeedbackEvents.V1.FeedbackSubmitted @event, CancellationToken ct);
}
