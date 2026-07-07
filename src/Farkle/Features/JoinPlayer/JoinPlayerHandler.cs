using Ardalis.Result;
using Farkle.Application;
using Farkle.Domain.GameAggregate;
using Wolverine.Marten;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Features.JoinPlayer;

public static class JoinPlayerHandler
{
  [AggregateHandler]
  public static (Result<JoinPlayerResponse>, Events) Handle(Command.JoinPlayer cmd, GameState state)
  {
    var events = JoinPlayerDecider.Decide(cmd, state).ToArray();
    return SliceResult.From(state, events, s =>
    {
      var lobby = LobbyMapper.ToLobbyState(s);
      return new JoinPlayerResponse(
        s.Players.Last().Id,
        s.PlayerInTurn,
        lobby.HostPlayerId,
        lobby.Stage,
        lobby.Roster);
    });
  }
}
