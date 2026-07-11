using Farkle.Domain.GameAggregate;
using Farkle.SharedKernel.Turns;
using FluentAssertions;

namespace Farkle.Tests.Framework;

public class LoadShould
{
  [Fact]
  public void RestoreGameStateFromEvents()
  {
    // Arrange
    var game   = new Game();
    var events = new[] { new GameEvents.V1.GameStarted(1) };

    // Act
    game.Load(events);

    // Assert
    game.State.GameStage.Should().Be(GameStage.WaitingForPlayers);
    game.State.Code.Should().Be(events[0].Id);
  }
}
