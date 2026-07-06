using Farkle.Domain;
using Farkle.Domain.GameAggregate;

namespace Farkle.Features.ReturnDice;

// Pure decision for the ReturnDice slice (#159): put a set-aside die back. Valid only if the die
// is currently set aside.
internal static class ReturnDiceDecider
{
  public static IEnumerable<object> Decide(Command.ReturnDice command, GameState state)
  {
    var validation = new PlayerIsInTurn(state, command.PlayerId)
      .And(new DieIsSetAside(state, command.PlayerId, command.Die.Value))
      .IsSatisfied();

    if (!validation.IsValid)
      return [validation.FailedValidationEvent];

    return [new GameEvents.V1.DiceReturned(command.PlayerId, command.Die.Value)];
  }
}
