using Farkle.Domain.GameAggregate;
using Farkle.Features.StartGame;
using Farkle.Tests.Framework;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents.V1;
using static Farkle.SharedKernel.Turns.GameStage;

namespace Farkle.Tests.Domain;

public class StartShould
{
  [Fact]
  public void ChangeStateToStarted()
  {
    // Arrange
    var gameId = 1;
    var game = new Game();

    // Act
    game.Start(new StartGameCommand(gameId));

    // Assert
    game.State.GameStage.Should().Be(WaitingForPlayers);
    game.State.Code.Should().Be(gameId);
  }

  [Fact]
  public void RaiseGameStartedEvent()
  {
    // Arrange
    var game = new Game();

    // Act
    game.Start(new StartGameCommand(1));

    // Assert
    game.Changes.Should().Contain(e => e is GameStarted);
  }

  [Fact]
  public void NotAllowAGameToStartTwice()
  {
    // Arrange
    var game = new Game();
    game.Start(new StartGameCommand(1));

    // Act
    game.Start(new StartGameCommand(1));

    // Assert
    game.Changes.Should().ContainSingleEvent<GameStarted>();
  }
}
