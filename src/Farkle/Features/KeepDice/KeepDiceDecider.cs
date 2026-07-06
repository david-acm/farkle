using Farkle.Domain;
using Farkle.Domain.GameAggregate;
using Farkle.SharedKernel.Scoring;

namespace Farkle.Features.KeepDice;

// Pure decision for the KeepDice slice: move scoring dice from the table into the hand and update
// the turn score. Validation-as-events: must be in turn, actually hold those dice, and they must
// score. Emits the V1.DiceKept the production command path uses (GameService wires game.KeepDice).
internal static class KeepDiceDecider
{
  public static IEnumerable<object> Decide(Command.KeepDice command, GameState state)
  {
    var dice = Dice.FromValues(command.DiceValues.ToPrimitiveArray());

    var validation = new PlayerIsInTurn(state, command.PlayerId)
      .And(new PlayerHasThoseDice(dice, state))
      .And(new CanKeepDice(dice))
      .IsSatisfied();

    if (!validation.IsValid)
      return [validation.FailedValidationEvent];

    return
    [
      new GameEvents.V1.DiceKept(
        command.PlayerId,
        command.DiceValues.ToPrimitiveArray(),
        TableCenterAfterKeep(command.DiceValues, state),
        NewTurnScore(command.DiceValues, state.TurnScore, state))
    ];
  }

  private static int[] TableCenterAfterKeep(IEnumerable<DieValue> kept, GameState state)
  {
    var remaining = state.TableCenter.RemoveRange(kept);
    // An empty table means the player kept everything and re-rolls all six next; the read model
    // signals that by echoing the full pre-keep table rather than an empty one.
    return remaining.IsEmpty
      ? state.TableCenter.AddRange(kept).ToPrimitiveArray()
      : remaining.ToPrimitiveArray();
  }

  // The turn-level combo rule: a second four-of-a-kind ("straight") in the same turn doubles the
  // running total. Everything else is the shared ScoreCalculator (single source of truth).
  private static Score NewTurnScore(IEnumerable<DieValue> diceKept, int currentScore, GameState state)
  {
    var breakdown = ScoreCalculator.Evaluate(diceKept.Select(d => d.Value).ToList());
    var turnScore = breakdown.Points;

    if (breakdown.Tricks.Contains(ScoringTrick.FourOfAKind) && state.StraightsKeptThisTurn > 0)
      return new Score((currentScore + turnScore) * 2);

    return new Score(currentScore + turnScore);
  }
}
