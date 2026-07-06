using Farkle.Domain.GameAggregate;
using Farkle.Features.ReturnDice;
using Farkle.SharedKernel.Turns;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Features.ReturnDice;

public class ReturnDiceDeciderShould
{
  // Player 1 in turn, having set the One aside from a [1,5,2,3,4,6] roll.
  private static GameState WithOneSetAside() => GameState.Fold(
    new V1.GameStarted(1),
    new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
    new V1.GamePlayStarted(1),
    new V2.DiceRolled(1, new[] { 1, 5, 2, 3, 4, 6 }, new Score(0), GameStage.Keeping),
    new V1.DiceSetAside(1, 1));

  [Fact]
  public void EmitDiceReturnedForASetAsideDie()
  {
    var events = ReturnDiceDecider.Decide(
      new Command.ReturnDice(1, 1, DieValue.One), WithOneSetAside());

    events.Should().ContainSingle().Which.Should().BeEquivalentTo(new V1.DiceReturned(1, 1));
  }

  [Fact]
  public void RejectReturningADieThatIsNotSetAside()
  {
    var events = ReturnDiceDecider.Decide(
      new Command.ReturnDice(1, 1, DieValue.Five), WithOneSetAside());

    events.Should().ContainSingle().Which.Should().BeEquivalentTo(new V1.DieNotSetAside(1, 5));
  }
}
