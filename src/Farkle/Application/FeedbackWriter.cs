using Farkle.Domain.Feedback;
using Marten;

namespace Farkle.Application;

// #277 — appends a FeedbackSubmitted fact to the per-session Marten stream "feedback-{sessionId}"
// (ADR 0004). Feedback is fire-and-forget with no version invariant, so a plain append on a fresh
// lightweight session suffices. Opens its own session because the writer is a singleton.
internal sealed class FeedbackWriter(IDocumentStore store) : IFeedbackWriter
{
  public async Task AppendAsync(string sessionId, FeedbackEvents.V1.FeedbackSubmitted @event, CancellationToken ct)
  {
    await using var session = store.LightweightSession();
    session.Events.Append($"feedback-{sessionId}", @event);
    await session.SaveChangesAsync(ct);
  }
}
