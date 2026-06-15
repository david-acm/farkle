using Farkle.Domain.GameAggregate;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.Endpoints;

internal static class PassTurnMapper
{
  // Builds the TurnChanged payload from game state. Shared by the HTTP endpoint
  // (response) and the broadcast subscription. `passedByPlayerId` is the player who
  // passed — the HTTP path takes it from the request, the subscription from the
  // V1.TurnPassed event.
  public static PassTurnResponse ToPassTurnResponse(GameState s, int passedByPlayerId) =>
    new(
      s.Id!.Id,
      passedByPlayerId,
      s.GameScoreFor(passedByPlayerId),
      s.Winner == null
        ? null
        : new WinnerResponse(s.Winner.Id, s.Winner.Name, s.GameScoreFor(s.Winner.Id)),
      s.PlayerInTurn,
      s.Players.Select(p => new PlayerScore(p.Id, p.Name, s.GameScoreFor(p.Id), p.Color)).ToArray());
}
