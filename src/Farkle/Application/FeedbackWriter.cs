using Eventuous;
using Farkle.Domain.Feedback;

namespace Farkle.Application;

// #277 — appends a FeedbackSubmitted fact to the per-session stream "Feedback-{sessionId}".
// ExpectedStreamVersion.Any: feedback is fire-and-forget, there's no version invariant to guard.
internal sealed class FeedbackWriter(IEventStore store) : IFeedbackWriter
{
  public Task AppendAsync(string sessionId, FeedbackEvents.V1.FeedbackSubmitted @event, CancellationToken ct) =>
    store.AppendEvents(
      new StreamName($"Feedback-{sessionId}"),
      ExpectedStreamVersion.Any,
      new[] { new NewStreamEvent(Guid.NewGuid(), @event, new Metadata()) },
      ct);
}
