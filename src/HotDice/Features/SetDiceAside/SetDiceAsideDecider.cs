using HotDice.Domain;
using HotDice.Domain.GameAggregate;

namespace HotDice.Features.SetDiceAside;

// Pure decision for the SetDiceAside slice (#159). Transient selection only — never scores or
// keeps; a die may be set aside only if a not-yet-set-aside copy is still on the table.
internal static class SetDiceAsideDecider
{
  public static IEnumerable<object> Decide(SetDiceAsideCommand command, GameState state)
  {
    var validation = new PlayerIsInTurn(state, command.PlayerId)
      .And(new DieIsAvailableToSetAside(state, command.PlayerId, command.Die.Value))
      .IsSatisfied();

    if (!validation.IsValid)
      return [validation.FailedValidationEvent];

    return [new GameEvents.V1.DiceSetAside(command.PlayerId, command.Die.Value)];
  }
}
