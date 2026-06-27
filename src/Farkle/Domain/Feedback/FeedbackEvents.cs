using Eventuous;

namespace Farkle.Domain.Feedback;

// #277 — user feedback is recorded as standalone facts, NOT through an aggregate: it has no
// cross-event invariants (each submission stands alone), so there's no state machine to protect.
// Events are appended directly to their own "Feedback-{sessionId}" stream (see IFeedbackWriter).
// Auto-registered with Eventuous' TypeMap via [EventType] (TypeMap.RegisterKnownEventTypes()).
internal static class FeedbackEvents
{
  internal static class V1
  {
    // A single feedback submission. Context (GameId/PlayerId/Stage/Route) is optional — present
    // in-game, absent on the landing/lobby pages. Sentiment is "Up" | "Down". No PII (no names).
    [EventType("V1.FeedbackSubmitted")]
    internal record FeedbackSubmitted(
      int?           GameId,
      int?           PlayerId,
      string?        Stage,
      string         Message,
      string         Sentiment,
      string?        Route,
      DateTimeOffset At);
  }
}
