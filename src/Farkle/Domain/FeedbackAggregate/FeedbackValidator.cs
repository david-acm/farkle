using static Farkle.Domain.FeedbackAggregate.FeedbackEvents.V1;

namespace Farkle.Domain.FeedbackAggregate;

internal static class FeedbackValidator
{
  internal const int MaxMessageLength = 2_000;

  // ValidationResult lives in the enclosing Farkle.Domain namespace, so it is in scope here.
  public static ValidationResult ValidatePreconditions(Feedback feedback, object @event)
  {
    return @event switch
    {
      FeedbackSubmitted e => Validate(
        !string.IsNullOrWhiteSpace(e.Message) && e.Message.Length <= MaxMessageLength,
        new EmptyFeedbackMessage(
          $"Feedback message must be non-empty and at most {MaxMessageLength} characters.")),

      // A rage-click signal is a passive observation — nothing to reject.
      _ => new ValidationResult(true, @event)
    };
  }

  private static ValidationResult Validate(bool valid, object failedValidationEvent) =>
    new(valid, failedValidationEvent);
}
