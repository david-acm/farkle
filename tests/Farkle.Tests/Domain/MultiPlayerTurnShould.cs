using Farkle.Domain.GameAggregate;
using Farkle.Features.KeepDice;
using Farkle.Features.PassTurn;
using Farkle.Features.RollDice;
using FluentAssertions;
using Xunit.Abstractions;
using static Farkle.Domain.GameAggregate.DieValue;

namespace Farkle.Tests.Domain;

public class MultiPlayerTurnShould : GameWithThreePlayersTest
{
  public MultiPlayerTurnShould(ITestOutputHelper output) : base(output)
  {
  }

  [Fact]
  public void RotateTurnsAcrossPlayersAndAccumulateScores()
  {
    // Arrange — capture the original player ids in join order (1, 2, 3)
    var p1 = State.Players[0].Id;
    var p2 = State.Players[1].Id;
    var p3 = State.Players[2].Id;

    // Act + Assert — Player 1 takes a turn (keeps a single 1 = 100)
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, p1));
    Game.KeepDice(new KeepDiceCommand(1, p1, new[] { One }));
    Game.PassTurn(new PassTurnCommand(1, p1));

    // Turn rotates to player 2; the order shifts player 1 to the back
    State.PlayerInTurn.Should().Be(p2);
    State.Players.Select(p => p.Id).Should().Equal(p2, p3, p1);

    // Player 2 takes a turn (keeps a single 5 = 50)
    SetupDiceToRoll(new List<int> { 5, 2, 3, 4, 6, 2 });
    Game.RollDiceV2(new RollDiceCommand(1, p2));
    Game.KeepDice(new KeepDiceCommand(1, p2, new[] { Five }));
    Game.PassTurn(new PassTurnCommand(1, p2));

    State.PlayerInTurn.Should().Be(p3);
    State.Players.Select(p => p.Id).Should().Equal(p3, p1, p2);

    // Player 3 takes a turn (keeps two 1s = 200)
    SetupDiceToRoll(new List<int> { 1, 1, 2, 3, 4, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, p3));
    Game.KeepDice(new KeepDiceCommand(1, p3, new[] { One, One }));
    Game.PassTurn(new PassTurnCommand(1, p3));

    // Turn returns to player 1; original order is restored
    State.PlayerInTurn.Should().Be(p1);
    State.Players.Select(p => p.Id).Should().Equal(p1, p2, p3);

    // Each player banks exactly the trick they kept on their own turn —
    // turn score must not leak across the turn boundary to the next player.
    State.GameScoreFor(new PlayerId(p1)).Should().Be(new Score(100));
    State.GameScoreFor(new PlayerId(p2)).Should().Be(new Score(50));
    State.GameScoreFor(new PlayerId(p3)).Should().Be(new Score(200));
  }
}
