using HotDice.Domain.GameAggregate;
using HotDice.Features.StartGame;
using HotDice.Tests.Framework;
using FluentAssertions;
using static HotDice.Domain.GameAggregate.GameEvents.V1;
using static HotDice.SharedKernel.Turns.GameStage;

namespace HotDice.Tests.Domain;

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
