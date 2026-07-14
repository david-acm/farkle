using Farkle.Domain.GameAggregate;
using Farkle.Features.SetDiceAside;
using Farkle.SharedKernel.Turns;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Features.SetDiceAside;

public class SetDiceAsideDeciderShould
{
  // Player 1 in turn with a roll of [1,5,2,3,4,6] on the table.
  private static GameState AfterRoll() => GameState.Fold(
    new V1.GameStarted(1),
    new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
    new V1.GamePlayStarted(1),
    new V2.DiceRolled(1, new[] { 1, 5, 2, 3, 4, 6 }, new Score(0), GameStage.Keeping));

  [Fact]
  public void EmitDiceSetAsideForADieOnTheTable()
  {
    var events = SetDiceAsideDecider.Decide(
      new SetDiceAsideCommand(1, 1, DieValue.One), AfterRoll());

    events.Should().ContainSingle().Which.Should().BeEquivalentTo(new V1.DiceSetAside(1, 1));
  }

  [Fact]
  public void RejectSettingAsideADieWithNoCopyLeftOnTheTable()
  {
    var oneAlreadySetAside = GameState.Fold(
      new V1.GameStarted(1),
      new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
      new V1.GamePlayStarted(1),
      new V2.DiceRolled(1, new[] { 1, 5, 2, 3, 4, 6 }, new Score(0), GameStage.Keeping),
      new V1.DiceSetAside(1, 1));

    var events = SetDiceAsideDecider.Decide(
      new SetDiceAsideCommand(1, 1, DieValue.One), oneAlreadySetAside);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.DieNotAvailableToSetAside(1, 1));
  }
}
