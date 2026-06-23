using Eventuous;
using static Farkle.Domain.FeedbackAggregate.FeedbackEvents;

namespace Farkle.Domain.FeedbackAggregate;

internal record FeedbackState : State<FeedbackState>
{
  public FeedbackState()
  {
    On<V1.RageClickDetected>(HandleRageClickDetected);
    On<V1.FeedbackSubmitted>(HandleFeedbackSubmitted);
  }

  // Running total of rage clicks seen this session (summed across bursts). Attached to a
  // submission so triage can see "how frustrated was this session before they spoke up".
  public int RageClickCount  { get; private init; }
  public int SubmissionCount { get; private init; }

  private static FeedbackState HandleRageClickDetected(FeedbackState state, V1.RageClickDetected e) =>
    state with { RageClickCount = state.RageClickCount + e.ClickCount };

  private static FeedbackState HandleFeedbackSubmitted(FeedbackState state, V1.FeedbackSubmitted e) =>
    state with { SubmissionCount = state.SubmissionCount + 1 };
}

// Per-session stream key (a client-generated, persisted session id), mirroring GameId's
// Eventuous.Id derivation. Streams are named Feedback-{Value}.
internal record FeedbackId(string Value) : Id(Value)
{
  public static readonly FeedbackId None = new("");
}
