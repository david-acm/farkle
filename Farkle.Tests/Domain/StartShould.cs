using Farkle.GameAggregate;
using FluentAssertions;
using static Farkle.GameAggregate.Command;
using static Farkle.GameAggregate.GameEvents.V1;
using static Farkle.GameAggregate.GameStage;

namespace Farkle.Tests.Domain;

public class StartShould
{
  [Fact]
  public void ChangeStateToStarted()
  {
    // Arrange
    var game = new Game();

    // Act
    var gameId = 1;
    game.Start(new Command.StartGame(gameId));

    // Assert
    game.State.GameStage.Should().Be(Rolling);
    game.State.Id.Should().Be((GameId)gameId);
  }

  [Fact]
  public void RaiseGameStartedEvent()
  {
    // Arrange
    var game = new Game();

    // Act
    game.Start(new Command.StartGame(1));

    // Assert
    game.Changes.Should().Contain(e => e is GameEvents.V1.GameStarted);
  }

  [Fact]
  public void NotAllowAGameToStartTwice()
  {
    // Arrange
    var game = new Game();

    // Act
    game.Start(new Command.StartGame(1));
    var secondStart = () => game.Start(new Command.StartGame(1));

    // Assert
    secondStart.Should().Throw<PreconditionsFailedException>();
  }
}
