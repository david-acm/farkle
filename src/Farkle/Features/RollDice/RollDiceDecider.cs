using Farkle.Domain;
using Farkle.Domain.GameAggregate;
using Farkle.SharedKernel.Turns;

namespace Farkle.Features.RollDice;

// Pure decision for the RollDice slice. The roll itself is a side effect (IRandom), so the
// already-rolled dice are passed in — the decision stays deterministic and mock-free to test.
// Score-after-roll is the current turn score if anything is keepable, else 0 (a farkle).
internal static class RollDiceDecider
{
  public static IEnumerable<object> Decide(Command.RollDice command, GameState state, Dice roll)
  {
    var validation = new PlayerIsInTurn(state, command.PlayerId)
      .And(new SingleRoll(state, command.PlayerId))
      .IsSatisfied();

    if (!validation.IsValid)
      return [validation.FailedValidationEvent];

    var scoreAfterRoll = new CanKeepDice(roll).IsSatisfied() ? state.TurnScore : new Score(0);

    return
    [
      new GameEvents.V2.DiceRolled(
        command.PlayerId,
        roll.DiceValues.ToPrimitiveArray(),
        scoreAfterRoll,
        GameStage.Keeping)
    ];
  }
}
