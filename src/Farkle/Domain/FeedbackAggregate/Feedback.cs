using Eventuous;
using static Farkle.Domain.FeedbackAggregate.FeedbackEvents;

namespace Farkle.Domain.FeedbackAggregate;

internal class Feedback : Aggregate<FeedbackState>
{
  public void RecordRageClick(Command.RecordRageClick cmd)
  {
    Apply(new V1.RageClickDetected(
      cmd.GameId, cmd.PlayerId, cmd.Stage, cmd.ClickCount, cmd.Element, cmd.Route, cmd.At));
  }

  public void SubmitFeedback(Command.SubmitFeedback cmd)
  {
    // RageClickCount comes from state (the session's accrued bursts), not the command.
    Apply(new V1.FeedbackSubmitted(
      cmd.GameId, cmd.PlayerId, cmd.Stage, cmd.Message, cmd.Sentiment,
      State.RageClickCount, cmd.Route, cmd.At));
  }

  // Validation-as-events: a failed precondition applies the validator's error event instead of
  // throwing, so the rejection is persisted and surfaced as an HTTP error by the application
  // layer (same pattern as Game.Apply).
  private void Apply(object @event)
  {
    var result = FeedbackValidator.ValidatePreconditions(this, @event);
    if (result.IsValid)
    {
      base.Apply(@event);
      return;
    }

    base.Apply(result.FailedValidationEvent);
  }
}
