using System.Collections.Generic;
using System.Linq;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Farkle.Tests.Application;

public class GameTelemetryShould
{
  [Fact]
  public void EmitAStructuredEventWithQueryableProperties()
  {
    // Arrange
    var logger = new CapturingLogger();
    var rolled = new GameEvents.V2.DiceRolled(PlayerId: 2, Dice: [1, 5, 3], TurnScore: new Score(150), Stage: GameStage.Keeping);

    // Act
    GameTelemetry.Log(logger, gameId: 42, @event: rolled, position: 7);

    // Assert — one Information event whose structured state carries queryable properties.
    var entry = logger.Entries.Should().ContainSingle().Subject;
    entry.Level.Should().Be(LogLevel.Information);
    entry.Properties.Should().Contain(p => p.Key == "EventType" && Equals(p.Value, "DiceRolled"));
    entry.Properties.Should().Contain(p => p.Key == "GameId" && Equals(p.Value, 42));
    // PlayerId is promoted to a top-level dimension (#219) so usage is queryable per player/seat.
    entry.Properties.Should().Contain(p => p.Key == "PlayerId" && Equals(p.Value, 2));
    entry.Properties.Should().Contain(p => p.Key == "Position");
    // The event itself is attached for destructuring (the "@" prefix is kept in the
    // Microsoft.Extensions.Logging state key and stripped by Serilog downstream), so its
    // fields become queryable properties in Azure Monitor.
    entry.Properties.Should().Contain(p => p.Key == "@GameEvent");
  }

  [Fact]
  public void LeavePlayerIdNull_ForEventsWithoutAPlayer()
  {
    var logger = new CapturingLogger();

    // GameStarted carries the game id, not a player — PlayerId must be null (absent dimension).
    GameTelemetry.Log(logger, gameId: 42, @event: new GameEvents.V1.GameStarted(42), position: 1);

    var entry = logger.Entries.Should().ContainSingle().Subject;
    entry.Properties.Should().Contain(p => p.Key == "PlayerId" && p.Value == null);
  }

  private sealed class CapturingLogger : ILogger
  {
    public List<(LogLevel Level, IReadOnlyList<KeyValuePair<string, object?>> Properties)> Entries { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
      Func<TState, Exception?, string> formatter)
    {
      var props = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
      Entries.Add((logLevel, props.ToList()));
    }

    private sealed class NullScope : IDisposable
    {
      public static readonly NullScope Instance = new();
      public void Dispose() { }
    }
  }
}
