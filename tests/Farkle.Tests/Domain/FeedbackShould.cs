using System.Linq;
using Farkle.Domain.FeedbackAggregate;
using FluentAssertions;
using static Farkle.Domain.FeedbackAggregate.Command;
using static Farkle.Domain.FeedbackAggregate.FeedbackEvents.V1;

namespace Farkle.Tests.Domain;

// #277 — the Feedback aggregate: a global, event-sourced feedback channel correlated to a game
// by an optional GameId. Records debounced rage-click bursts and thumbs-rated submissions.
public class FeedbackShould
{
  private static readonly FeedbackId Session = new("session-abc");

  private static RecordRageClick Rage(int clicks, int? gameId = null) =>
    new(Session, gameId, PlayerId: gameId is null ? null : 1, Stage: gameId is null ? null : "Rolling",
        ClickCount: clicks, Element: "button.roll", Route: gameId is null ? "/" : $"/games/{gameId}",
        At: DateTimeOffset.UtcNow);

  private static SubmitFeedback Submit(string message, Sentiment sentiment, int? gameId = null) =>
    new(Session, gameId, PlayerId: gameId is null ? null : 1, Stage: gameId is null ? null : "Keeping",
        Message: message, Sentiment: sentiment, Route: gameId is null ? "/" : $"/games/{gameId}",
        At: DateTimeOffset.UtcNow);

  [Fact]
  public void RecordARageClickBurstAsASingleEvent()
  {
    var feedback = new Feedback();

    feedback.RecordRageClick(Rage(clicks: 5, gameId: 123));

    var e = feedback.Changes.Should().ContainSingle().Which.Should().BeOfType<RageClickDetected>().Subject;
    e.ClickCount.Should().Be(5);
    e.GameId.Should().Be(123);
  }

  [Fact]
  public void CaptureFeedbackWithMessageSentimentAndContext()
  {
    var feedback = new Feedback();

    feedback.SubmitFeedback(Submit("Dice felt laggy", Sentiment.Down, gameId: 123));

    var e = feedback.Changes.Should().ContainSingle().Which.Should().BeOfType<FeedbackSubmitted>().Subject;
    e.Message.Should().Be("Dice felt laggy");
    e.Sentiment.Should().Be(Sentiment.Down);
    e.GameId.Should().Be(123);
    e.PlayerId.Should().Be(1);
  }

  [Fact]
  public void AttachTheRunningRageClickCountTakenFromStateNotTheCommand()
  {
    var feedback = new Feedback();
    feedback.RecordRageClick(Rage(clicks: 4));
    feedback.RecordRageClick(Rage(clicks: 3));

    feedback.SubmitFeedback(Submit("ugh", Sentiment.Down));

    feedback.Changes.OfType<FeedbackSubmitted>().Single().RageClickCount.Should().Be(7);
  }

  [Fact]
  public void AllowSubmittingFeedbackOffAGameWithNoCorrelation()
  {
    var feedback = new Feedback();

    feedback.SubmitFeedback(Submit("love it", Sentiment.Up));

    var e = feedback.Changes.OfType<FeedbackSubmitted>().Single();
    e.GameId.Should().BeNull();
    e.PlayerId.Should().BeNull();
    e.Sentiment.Should().Be(Sentiment.Up);
  }

  [Theory]
  [InlineData("")]
  [InlineData("   ")]
  public void RejectAnEmptyMessageAsAnErrorEvent(string message)
  {
    var feedback = new Feedback();

    feedback.SubmitFeedback(Submit(message, Sentiment.Up));

    feedback.Changes.Should().ContainSingle().Which.Should().BeOfType<EmptyFeedbackMessage>();
  }

  [Fact]
  public void RejectAnOverlongMessageAsAnErrorEvent()
  {
    var feedback = new Feedback();

    feedback.SubmitFeedback(Submit(new string('x', FeedbackValidator.MaxMessageLength + 1), Sentiment.Down));

    feedback.Changes.Should().ContainSingle().Which.Should().BeOfType<EmptyFeedbackMessage>();
  }
}
