using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eventuous;
using Farkle.Application;
using Farkle.Domain.Feedback;
using FluentAssertions;
using Xunit;

namespace Farkle.Tests.Application;

public class FeedbackWriterShould
{
  [Fact]
  public async Task AppendTheSubmissionToTheSessionStream_AnyVersion()
  {
    var store  = new CapturingEventStore();
    var writer = new FeedbackWriter(store);
    var @event = new FeedbackEvents.V1.FeedbackSubmitted(
      GameId: 42, PlayerId: 2, Stage: "Rolling", Message: "Dice felt unfair",
      Sentiment: "Down", Route: "/games/42", At: DateTimeOffset.UnixEpoch);

    await writer.AppendAsync("abc123", @event, CancellationToken.None);

    // Stream keyed by the client session id; no version invariant (fire-and-forget append).
    store.Stream.ToString().Should().Be("Feedback-abc123");
    store.ExpectedVersion.Should().Be(ExpectedStreamVersion.Any);

    // The domain event is passed as the payload object — the (traced) store serializes it.
    var appended = store.Events.Should().ContainSingle().Subject;
    appended.Payload.Should().BeSameAs(@event);
  }

  // Minimal IEventStore capturing the single append; the writer never reads, so reads throw.
  private sealed class CapturingEventStore : IEventStore
  {
    public StreamName                          Stream          { get; private set; }
    public ExpectedStreamVersion               ExpectedVersion { get; private set; }
    public IReadOnlyCollection<NewStreamEvent> Events          { get; private set; } = [];

    public Task<AppendEventsResult> AppendEvents(
      StreamName stream, ExpectedStreamVersion expectedVersion,
      IReadOnlyCollection<NewStreamEvent> events, CancellationToken cancellationToken)
    {
      Stream          = stream;
      ExpectedVersion = expectedVersion;
      Events          = events;
      return Task.FromResult(new AppendEventsResult((ulong)events.Count, events.Count - 1));
    }

    public Task<StreamEvent[]> ReadEvents(StreamName s, StreamReadPosition start, int count, CancellationToken ct) =>
      throw new NotSupportedException();
    public Task<StreamEvent[]> ReadEventsBackwards(StreamName s, StreamReadPosition start, int count, CancellationToken ct) =>
      throw new NotSupportedException();
    public Task<bool> StreamExists(StreamName s, CancellationToken ct) => throw new NotSupportedException();
    public Task DeleteStream(StreamName s, ExpectedStreamVersion v, CancellationToken ct) => throw new NotSupportedException();
    public Task TruncateStream(StreamName s, StreamTruncatePosition p, ExpectedStreamVersion v, CancellationToken ct) =>
      throw new NotSupportedException();
  }
}
