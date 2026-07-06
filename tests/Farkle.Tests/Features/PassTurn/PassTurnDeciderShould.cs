using System.Collections.Immutable;
using Farkle.Domain.GameAggregate;
using Farkle.Features.PassTurn;
using Farkle.SharedKernel.Turns;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Features.PassTurn;

public class PassTurnDeciderShould
{
  // Player 1 in turn, having rolled (so HasActedThisTurn is set); two players.
  private static GameState AfterRolling() => GameState.Fold(
    new V1.GameStarted(1),
    new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
    new V2.PlayerJoined(2, "Bob", PlayerColors.For(2)),
    new V1.GamePlayStarted(1),
    new V2.DiceRolled(1, new[] { 2, 3, 4, 6, 2, 3 }, new Score(0), GameStage.Keeping));

  [Fact]
  public void BankTheScoreAndRotateToTheNextPlayer()
  {
    var events = PassTurnDecider.Decide(new Command.PassTurn(1, 1), AfterRolling());

    var passed = events.Should().ContainSingle().Which.Should().BeOfType<V1.TurnPassed>().Subject;
    passed.PlayerId.Should().Be(1);
    passed.GameScore.Should().Be(0);
    passed.PlayerOrder.Select(p => p.Id).Should().Equal(2, 1); // passing player moves to the back
  }

  [Fact]
  public void RejectPassingBeforeActingThisTurn()
  {
    var freshTurn = GameState.Fold(
      new V1.GameStarted(1),
      new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
      new V2.PlayerJoined(2, "Bob", PlayerColors.For(2)),
      new V1.GamePlayStarted(1));

    var events = PassTurnDecider.Decide(new Command.PassTurn(1, 1), freshTurn);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.PassedWithoutRolling(1));
  }

  [Fact]
  public void RejectPassingOutOfTurn()
  {
    var events = PassTurnDecider.Decide(new Command.PassTurn(1, 2), AfterRolling());

    events.Should().ContainSingle().Which.Should().BeEquivalentTo(new V1.PlayedOutOfTurn(2, 1));
  }

  [Fact]
  public void DeclareAWinWhenTheBankedScoreReachesTheThreshold()
  {
    // Single player already banked 4900; a 200-point turn tips them over 5000.
    var nearWin = GameState.Fold(
      new V1.GameStarted(1),
      new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
      new V1.GamePlayStarted(1),
      new V1.TurnPassed(1, new[] { new Player(1, "Alice") }.ToImmutableArray(), 4900),
      new V2.DiceRolled(1, new[] { 1, 1, 5, 2, 3, 4 }, new Score(0), GameStage.Keeping),
      new V1.DiceKept(1, new[] { 1, 1 }, new[] { 5, 2, 3, 4 }, 200));

    var events = PassTurnDecider.Decide(new Command.PassTurn(1, 1), nearWin).ToList();

    events.Should().HaveCount(2);
    events.Should().ContainEquivalentOf(new V1.GameWon(1, 5100));
  }
}
