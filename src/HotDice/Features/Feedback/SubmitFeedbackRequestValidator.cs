using FluentValidation;
using static HotDice.Contracts.HttpRequests;

namespace HotDice.Features.Feedback;

// Input validation for the SubmitFeedback slice (#302 follow-up). Replaces the endpoint's hand-rolled
// checks with the same rules, now run by the Wolverine.HTTP FluentValidation middleware → 400 before
// the endpoint. Sentiment reuses the endpoint's NormalizeSentiment so the accepted set (the words or
// the glyphs) stays a single source of truth. Public so Wolverine injects it into the generated
// handler (see AddHotDiceModuleServices for the singleton registration rationale).
public sealed class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
{
  public SubmitFeedbackRequestValidator()
  {
    RuleFor(x => x.SessionId).NotEmpty()
      .WithMessage("A feedback session id is required.");

    RuleFor(x => x.Message).NotEmpty()
      .WithMessage("Feedback message cannot be empty.")
      .MaximumLength(SubmitFeedbackEndpoint.MaxMessageLength)
      .WithMessage($"Feedback message cannot exceed {SubmitFeedbackEndpoint.MaxMessageLength} characters.");

    RuleFor(x => x.Sentiment)
      .Must(s => SubmitFeedbackEndpoint.NormalizeSentiment(s) is not null)
      .WithMessage("Sentiment must be 'Up' or 'Down'.");
  }
}
