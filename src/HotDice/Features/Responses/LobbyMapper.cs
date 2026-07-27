using HotDice.Domain.GameAggregate;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.Features;

internal static class LobbyMapper
{
  public static LobbyStateResponse ToLobbyState(GameState s) =>
    new(
      s.Code,
      s.GameStage.ToString(),
      Roster: s.Players.Select(p => new LobbyPlayer(p.Id, p.Name, p.Color)).ToArray(),
      HostPlayerId: s.Players.IsEmpty ? 0 : s.Players[0].Id,
      CurrentPlayerId: s.PlayerInTurn,
      TurnNumber: s.TurnNumber);
}
