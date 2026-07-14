using Farkle.Domain.GameAggregate;
using Farkle.Features.RollDice;
using Farkle.SharedKernel.Turns;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Features.RollDice;

public class RollDiceDeciderShould
{
  // A game in play with player 1 in turn, awaiting a roll (Rolling stage).
  private static GameState InTurnAwaitingRoll() => GameState.Fold(
    new V1.GameStarted(1),
    new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
    new V1.GamePlayStarted(1));

  [Fact]
  public void EmitDiceRolledMovingToKeeping()
  {
    var roll = Dice.FromValues(new[] { 1, 5, 2, 3, 4, 6 });

    var events = RollDiceDecider.Decide(new RollDiceCommand(1, 1), InTurnAwaitingRoll(), roll);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(
        new V2.DiceRolled(1, new[] { 1, 5, 2, 3, 4, 6 }, new Score(0), GameStage.Keeping));
  }

  [Fact]
  public void RejectRollingOutOfTurn()
  {
    var roll = Dice.FromValues(new[] { 1, 5, 2, 3, 4, 6 });

    var events = RollDiceDecider.Decide(new RollDiceCommand(1, 2), InTurnAwaitingRoll(), roll);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.PlayedOutOfTurn(2, 1));
  }

  [Fact]
  public void RejectASecondRollBeforeKeeping()
  {
    var afterRoll = GameState.Fold(
      new V1.GameStarted(1),
      new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
      new V1.GamePlayStarted(1),
      new V2.DiceRolled(1, new[] { 1, 5, 2, 3, 4, 6 }, new Score(0), GameStage.Keeping));

    var events = RollDiceDecider.Decide(
      new RollDiceCommand(1, 1), afterRoll, Dice.FromValues(new[] { 1, 1, 1, 1, 1, 1 }));

    events.Should().ContainSingle().Which.Should().BeEquivalentTo(new V1.RolledTwice(1));
  }
}
