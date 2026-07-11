using Ardalis.Result;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.SetDiceAside;

public static class SetDiceAsideHandler
{
  [AggregateHandler]
  public static (Result<SetAsideResponse>, Events) Handle(Command.SetDiceAside cmd, GameState state)
  {
    var events = SetDiceAsideDecider.Decide(cmd, state).ToArray();
    return SliceResult.From(state, events,
      s => new SetAsideResponse(s.Code, s.DiceSetAside.Select(d => d.Value).ToArray()));
  }
}
