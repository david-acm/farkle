using Ardalis.Result;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.PassTurn;

public static class PassTurnHandler
{
  [AggregateHandler]
  public static (Result<PassTurnResponse>, Events) Handle(Command.PassTurn cmd, GameState state)
  {
    var events = PassTurnDecider.Decide(cmd, state).ToArray();
    return SliceResult.From(state, events, s => PassTurnMapper.ToPassTurnResponse(s, cmd.PlayerId));
  }
}
