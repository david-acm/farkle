using Farkle.Domain.GameAggregate;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.Command;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

namespace Farkle.Tests.Domain;

public class JoinShould
{
  [Fact]
  public void AddPlayerToGame()
  {
    // Arrange
    var game = new Game();
    game.Start(new StartGame(1));
    var player1 = new JoinPlayer(1, 1, "David");

    // Act
    game.JoinPlayer(player1);
    game.JoinPlayer(new JoinPlayer(1, 2, "Cristian"));

    // Assert
    game.Changes.Where(p => p is PlayerJoined).Should().HaveCount(2);
    game.State.Players.Should().Contain(new Player(1, "David"));
  }
}
