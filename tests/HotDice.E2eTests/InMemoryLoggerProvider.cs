using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace HotDice.E2eTests;

/// <summary>
/// Buffers Web API log entries in memory so they can be flushed to disk when a test fails.
/// </summary>
public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public ILogger CreateLogger(string categoryName) =>
        new InMemoryLogger(categoryName, _entries);

    /// <summary>Removes and returns all buffered entries, leaving the queue empty.</summary>
    public IReadOnlyList<string> Drain()
    {
        var result = new List<string>();
        while (_entries.TryDequeue(out var entry))
            result.Add(entry);
        return result;
    }

    public void Dispose() { }
}

file sealed class InMemoryLogger(string category, ConcurrentQueue<string> entries) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel level) => level >= LogLevel.Information;

    public void Log<TState>(LogLevel level, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level,-11}] [{category}] {formatter(state, exception)}";
        entries.Enqueue(line);
        if (exception != null)
            entries.Enqueue($"  {exception}");
    }
}
