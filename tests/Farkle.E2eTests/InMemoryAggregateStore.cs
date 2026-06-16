using System.Collections.Concurrent;
using Eventuous;
using Farkle.Domain.GameAggregate;

namespace Farkle.E2eTests;

// In-memory implementation of Eventuous' IEventStore (IEventReader + IEventWriter). It keeps the
// raw event payloads per stream in a dictionary; Eventuous' LoadAggregate/StoreAggregate
// extensions replay them onto freshly constructed aggregates — preserving the entire real domain
// (lobby/begin, rolling, scoring, pass) while removing the EventStore (ESDB) dependency.
//
// As of Eventuous 0.15.1 the aggregate store (IAggregateStore) was retired in favour of the
// lower-level IEventReader/IEventWriter contract, so the store only deals in StreamEvent /
// NewStreamEvent envelopes here (the Game aggregate is rehydrated by the framework, not us).
//
// The deterministic IRandom (ScriptedRandom) is supplied via the aggregate factory the host
// registers, so every roll yields a scoring die and the storyboard stages stay reproducible.
internal sealed class InMemoryAggregateStore : IEventStore
{
    private readonly ConcurrentDictionary<string, List<object>> _streams = new();

    public Task<AppendEventsResult> AppendEvents(
        StreamName stream,
        ExpectedStreamVersion expectedVersion,
        IReadOnlyCollection<NewStreamEvent> events,
        CancellationToken cancellationToken)
    {
        var list = _streams.GetOrAdd(stream.ToString(), _ => new List<object>());
        lock (list)
        {
            foreach (var e in events)
                list.Add(e.Payload!);

            return Task.FromResult(new AppendEventsResult((ulong)list.Count, list.Count - 1));
        }
    }

    public Task<StreamEvent[]> ReadEvents(
        StreamName stream,
        StreamReadPosition start,
        int count,
        CancellationToken cancellationToken)
    {
        if (!_streams.TryGetValue(stream.ToString(), out var list))
            throw new StreamNotFound(stream);

        List<object> snapshot;
        lock (list) snapshot = new List<object>(list);

        var events = snapshot
            .Skip((int)start.Value)
            .Take(count)
            .Select((payload, i) => ToStreamEvent(payload, start.Value + i))
            .ToArray();

        return Task.FromResult(events);
    }

    public Task<StreamEvent[]> ReadEventsBackwards(
        StreamName stream,
        StreamReadPosition start,
        int count,
        CancellationToken cancellationToken)
    {
        if (!_streams.TryGetValue(stream.ToString(), out var list))
            throw new StreamNotFound(stream);

        List<object> snapshot;
        lock (list) snapshot = new List<object>(list);

        var events = snapshot
            .Select((payload, i) => ToStreamEvent(payload, i))
            .Reverse()
            .Take(count)
            .ToArray();

        return Task.FromResult(events);
    }

    public Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) =>
        Task.FromResult(_streams.ContainsKey(stream.ToString()));

    // The game flow never deletes or truncates streams; implement them faithfully anyway so the
    // double satisfies the full IEventStore contract.
    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken)
    {
        _streams.TryRemove(stream.ToString(), out _);
        return Task.CompletedTask;
    }

    public Task TruncateStream(
        StreamName stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion expectedVersion,
        CancellationToken cancellationToken)
    {
        if (_streams.TryGetValue(stream.ToString(), out var list))
            lock (list) list.RemoveRange(0, Math.Min((int)truncatePosition.Value, list.Count));

        return Task.CompletedTask;
    }

    private static StreamEvent ToStreamEvent(object payload, long position) =>
        new(Guid.NewGuid(), payload, new Metadata(), "application/json", position, FromArchive: false);
}
