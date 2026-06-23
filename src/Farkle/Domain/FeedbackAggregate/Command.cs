namespace Farkle.Domain.FeedbackAggregate;

internal static class Command
{
  // Records one debounced rage-click burst. GameId/PlayerId/Stage are optional (the icon is
  // global). FeedbackId is the per-session stream key; At is the client timestamp of the burst.
  internal record RecordRageClick(
    FeedbackId     FeedbackId,
    int?           GameId,
    int?           PlayerId,
    string?        Stage,
    int            ClickCount,
    string?        Element,
    string?        Route,
    DateTimeOffset At);

  // Submits feedback. The running rage-click count is taken from aggregate state, not the
  // command, so it can't be spoofed by the client.
  internal record SubmitFeedback(
    FeedbackId     FeedbackId,
    int?           GameId,
    int?           PlayerId,
    string?        Stage,
    string         Message,
    Sentiment      Sentiment,
    string?        Route,
    DateTimeOffset At);
}
