using Farkle.Domain.GameAggregate;
using Farkle.Features.JoinPlayer;
using Farkle.Features.StartGame;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents.V2;

namespace Farkle.Tests.Domain;

public class JoinShould
{
  [Fact]
  public void AddPlayerToGame()
  {
    // Arrange
    var game = new Game();
    game.Start(new StartGameCommand(1));
    var player1 = new JoinPlayerCommand(1, "David");

    // Act
    game.JoinPlayer(player1);
    game.JoinPlayer(new JoinPlayerCommand(1, "Cristian"));

    // Assert
    game.Changes.Where(p => p is PlayerJoined).Should().HaveCount(2);
    game.State.Players.Should().Contain(new Player(1, "David", PlayerColors.For(1)));
  }
}
