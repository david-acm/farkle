using Farkle.GameAggregate;
using FluentAssertions;
using static Farkle.GameAggregate.Command;
using static Farkle.GameAggregate.GameEvents.V1;

namespace Farkle.Tests.Domain;

public class JoinShould
{
  [Fact]
  public void AddPlayerToGame()
  {
    // Arrange
    var game = new Game();

    // Act
    game.Start(new Command.StartGame(1));
    var player1 = new Command.JoinPlayer(1, 1, "David");
    game.JoinPlayer(player1);
    game.JoinPlayer(new Command.JoinPlayer(1, 2, "Cristian"));

    // Assert
    game.Changes.Where(p => p is GameEvents.V1.PlayerJoined).Should().HaveCount(2);
    game.State.Players.Should().Contain(new Player(1, "David"));
  }
}
