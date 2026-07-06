using Farkle.Domain.GameAggregate;
using Farkle.Features.KeepDice;
using Farkle.SharedKernel.Turns;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Features.KeepDice;

public class KeepDiceDeciderShould
{
  // Player 1 in turn with [1,5,2,3,4,6] rolled on the table.
  private static GameState AfterRoll() => GameState.Fold(
    new V1.GameStarted(1),
    new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
    new V1.GamePlayStarted(1),
    new V2.DiceRolled(1, new[] { 1, 5, 2, 3, 4, 6 }, new Score(0), GameStage.Keeping));

  [Fact]
  public void KeepAScoringDieAndScoreIt()
  {
    var events = KeepDiceDecider.Decide(
      new Command.KeepDice(1, 1, new[] { DieValue.One }), AfterRoll());

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(
        new V1.DiceKept(1, new[] { 1 }, new[] { 5, 2, 3, 4, 6 }, 100));
  }

  [Fact]
  public void RejectKeepingDiceThePlayerDoesNotHave()
  {
    // Only one 1 is on the table; keeping two is not allowed.
    var events = KeepDiceDecider.Decide(
      new Command.KeepDice(1, 1, new[] { DieValue.One, DieValue.One }), AfterRoll());

    events.Should().ContainSingle().Which.Should().BeOfType<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void RejectKeepingNonScoringDice()
  {
    var events = KeepDiceDecider.Decide(
      new Command.KeepDice(1, 1, new[] { DieValue.Two }), AfterRoll());

    events.Should().ContainSingle().Which.Should().BeOfType<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void RejectKeepingOutOfTurn()
  {
    var events = KeepDiceDecider.Decide(
      new Command.KeepDice(1, 2, new[] { DieValue.One }), AfterRoll());

    events.Should().ContainSingle().Which.Should().BeEquivalentTo(new V1.PlayedOutOfTurn(2, 1));
  }
}
