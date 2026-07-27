using HotDice.Features.Feedback;
using FluentAssertions;
using static HotDice.Contracts.HttpRequests;

namespace HotDice.Tests.Features.Feedback;

// The SubmitFeedback slice validates input structure through FluentValidation (run by the
// Wolverine.HTTP middleware) rather than validation-as-events, since it has no aggregate. These pin
// the rules that used to live inline in the endpoint: a session id and a non-blank, bounded message
// are required, and the sentiment must be a recognised word or glyph.
public class SubmitFeedbackRequestValidatorShould
{
  private readonly SubmitFeedbackRequestValidator _validator = new();

  private static SubmitFeedbackRequest Valid(
    string session = "sess-1", string message = "Nice dice", string sentiment = "Up") =>
    new(session, message, sentiment);

  [Fact]
  public void AcceptAWellFormedRequest() =>
    _validator.Validate(Valid()).IsValid.Should().BeTrue();

  [Theory]
  [InlineData("up")]
  [InlineData("Down")]
  [InlineData("👍")]
  [InlineData("👎")]
  public void AcceptEitherSentimentWordOrGlyph(string sentiment) =>
    _validator.Validate(Valid(sentiment: sentiment)).IsValid.Should().BeTrue();

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectABlankSessionId(string session) =>
    _validator.Validate(Valid(session: session)).IsValid.Should().BeFalse();

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectABlankMessage(string message) =>
    _validator.Validate(Valid(message: message)).IsValid.Should().BeFalse();

  [Fact]
  public void RejectAnOversizedMessage() =>
    _validator.Validate(Valid(message: new string('x', SubmitFeedbackEndpoint.MaxMessageLength + 1)))
      .IsValid.Should().BeFalse();

  [Theory]
  [InlineData("sideways")]
  [InlineData("")]
  [InlineData("🤷")]
  public void RejectAnUnrecognisedSentiment(string sentiment) =>
    _validator.Validate(Valid(sentiment: sentiment)).IsValid.Should().BeFalse();
}
