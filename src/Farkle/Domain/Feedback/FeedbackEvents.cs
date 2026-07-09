namespace Farkle.Domain.Feedback;

// #277 — user feedback is recorded as standalone facts, NOT through an aggregate: it has no
// cross-event invariants (each submission stands alone), so there's no state machine to protect.
// Events are appended directly to their own "feedback-{sessionId}" Marten stream (see IFeedbackWriter).
// Marten registers the event type by CLR type (no Eventuous [EventType] needed, ADR 0004).
internal static class FeedbackEvents
{
  internal static class V1
  {
    // A single feedback submission. Context (GameId/PlayerId/Stage/Route) is optional — present
    // in-game, absent on the landing/lobby pages. Sentiment is "Up" | "Down". No PII (no names).
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
