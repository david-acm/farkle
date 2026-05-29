using Farkle.Domain.GameAggregate;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal static class LobbyMapper
{
  public static LobbyStateResponse ToLobbyState(GameState s) =>
    new(
      s.Id!.Id,
      s.GameStage.ToString(),
      s.Players.Select(p => new LobbyPlayer(p.Id, p.Name)).ToArray(),
      s.Players.IsEmpty ? 0 : s.Players[0].Id,
      s.PlayerInTurn);
}
