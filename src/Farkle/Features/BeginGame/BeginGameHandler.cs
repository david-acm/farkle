using Ardalis.Result;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.BeginGame;

public static class BeginGameHandler
{
  [AggregateHandler]
  public static (Result<LobbyStateResponse>, Events) Handle(Command.BeginGame cmd, GameState state)
  {
    var events = BeginGameDecider.Decide(cmd, state).ToArray();
    return SliceResult.From(state, events, LobbyMapper.ToLobbyState);
  }
}
