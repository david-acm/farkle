using Ardalis.Result;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.KeepDice;

public static class KeepDiceHandler
{
  [AggregateHandler]
  public static (Result<KeepDiceResponse>, Events) Handle(Command.KeepDice cmd, GameState state)
  {
    var events = KeepDiceDecider.Decide(cmd, state).ToArray();
    return SliceResult.From(state, events, s => new KeepDiceResponse(s.Code, s.TurnScore));
  }
}
