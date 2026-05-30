using Farkle.Domain.GameAggregate;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal static class PassTurnMapper
{
  public static PassTurnResponse ToPassTurn(GameState s, int passerPlayerId) =>
    new(
      s.Id!.Id,
      passerPlayerId,
      s.GameScoreFor(passerPlayerId),
      s.Winner == null ? null : new WinnerResponse(s.Winner.Id, s.Winner.Name, s.GameScoreFor(s.Winner.Id)),
      s.PlayerInTurn,
      s.Players.Select(p => new PlayerScore(p.Id, p.Name, s.GameScoreFor(p.Id))).ToArray());
}
