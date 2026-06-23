using Eventuous;

namespace Farkle.Domain.FeedbackAggregate;

// A thumbs-up / thumbs-down reaction on a feedback submission (#277). Kept deliberately
// coarse (not a 1–5 rating) — the free-text message carries the detail.
internal enum Sentiment
{
  Up,
  Down
}

// Feedback is its own event-sourced aggregate, separate from Game but correlated to it by an
// optional GameId carried on each event (the floating feedback icon is global, so a rage burst
// or submission can happen on any page — with or without a game in context). One aggregate per
// client feedback session (FeedbackId), so a session's rage signals and submissions accumulate
// in a single Feedback-{sessionId} stream.
internal static class FeedbackEvents
{
  internal static class V1
  {
    // One event per detected rage-click burst (the client debounces a burst to a single signal).
    // Context fields are optional because the burst may occur off a game (landing/lobby).
    [EventType("Feedback.V1.RageClickDetected")]
    internal record RageClickDetected(
      int?           GameId,
      int?           PlayerId,
      string?        Stage,
      int            ClickCount,
      string?        Element,
      string?        Route,
      DateTimeOffset At);

    // A submitted piece of feedback: the free-text message plus a thumbs sentiment and the
    // auto-attached context (game/player/stage/route, and how many rage clicks the session had
    // accrued before submitting — taken from state, not the request).
    [EventType("Feedback.V1.FeedbackSubmitted")]
    internal record FeedbackSubmitted(
      int?           GameId,
      int?           PlayerId,
      string?        Stage,
      string         Message,
      Sentiment      Sentiment,
      int            RageClickCount,
      string?        Route,
      DateTimeOffset At);

    // Validation-as-events: an empty/over-long message rejects the submission by applying this
    // error event instead, which the application layer surfaces as an HTTP error.
    [EventType("Feedback.V1.EmptyFeedbackMessage")]
    internal record EmptyFeedbackMessage(string Reason) : Farkle.Domain.GameAggregate.IErrorEvent;
  }
}
