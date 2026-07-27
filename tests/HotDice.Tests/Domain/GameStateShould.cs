using HotDice.Domain.GameAggregate;
using FluentAssertions;

namespace HotDice.Tests.Domain;

public class GameStateShould
{
  [Fact]
  public void ReturnZeroForPlayerInTurnWhenNoPlayersHaveJoined()
  {
    var state = new GameState();

    var act = () => state.PlayerInTurn;

    act.Should().NotThrow().And.Subject().Should().Be(0);
  }

  [Fact]
  public void ReturnZeroScoreForUnknownPlayer()
  {
    var state = new GameState();

    var score = state.GameScoreFor(99);

    score.Value.Should().Be(0);
  }
}
