using System.Collections.Concurrent;
using Eventuous;
using Farkle.Domain.GameAggregate;

namespace Farkle.E2eTests;

// In-memory implementation of Eventuous' IAggregateStore. It keeps the raw event
// objects per stream in a dictionary and replays them onto freshly constructed
// aggregates — preserving the entire real domain (lobby/begin, rolling, scoring,
// pass) while removing the EventStore (ESDB) dependency.
//
// Only the three StreamName-based members are abstract on IAggregateStore; the
// {T,TId} overloads are default interface methods that funnel into these.
//
// Game aggregates are built with a deterministic IRandom (ScriptedRandom) so every
// roll yields a scoring die, making the storyboard stages reproducible.
internal sealed class InMemoryAggregateStore : IAggregateStore
{
    private static readonly IRandom Dice = new ScriptedRandom();
    private readonly ConcurrentDictionary<string, List<object>> _streams = new();
    private readonly AggregateFactoryRegistry _factories = new();

    public InMemoryAggregateStore()
    {
        // Build Game aggregates with the deterministic dice source; other aggregate
        // types (none in this app) fall back to their parameterless constructor.
        _factories.CreateAggregateUsing<Game>(() => new Game(Dice));
    }

    public Task<AppendEventsResult> Store<T>(StreamName streamName, T aggregate, CancellationToken cancellationToken)
        where T : Aggregate
    {
        var stream = _streams.GetOrAdd(streamName.ToString(), _ => new List<object>());
        lock (stream)
        {
            stream.AddRange(aggregate.Changes);
            return Task.FromResult(new AppendEventsResult((ulong)stream.Count, stream.Count - 1));
        }
    }

    public Task<T> Load<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => Task.FromResult(Rehydrate<T>(streamName));

    public Task<T> LoadOrNew<T>(StreamName streamName, CancellationToken cancellationToken) where T : Aggregate
        => Task.FromResult(Rehydrate<T>(streamName));

    private T Rehydrate<T>(StreamName streamName) where T : Aggregate
    {
        var aggregate = _factories.CreateInstance<T>();
        if (_streams.TryGetValue(streamName.ToString(), out var events))
        {
            List<object> snapshot;
            lock (events) snapshot = new List<object>(events);
            aggregate.Load(snapshot);
        }

        return aggregate;
    }
}
