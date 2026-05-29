namespace Farkle.Contracts;

public static class HttpResponses
{
  public record JoinPlayerResponse(
    int Id,
    int CurrentPlayerId,
    int HostPlayerId = 0,
    string Stage = "",
    IReadOnlyList<LobbyPlayer>? Players = null);

  public record LobbyPlayer(int PlayerId, string Name);

  public record LobbyStateResponse(
    int GameId,
    string Stage,
    IReadOnlyList<LobbyPlayer> Players,
    int HostPlayerId,
    int CurrentPlayerId);

  public record PlayerScore(int PlayerId, string Name, int Score);
  
  public record KeepDiceResponse(int Id, int TurnScore);
  
  public record RollDiceResponse(int Id, int[] DiceValues);
  
  public record StartGameResponse(int Id);

  public record PassTurnResponse(int GameId, int PlayerId, int NewScore, WinnerResponse? Winner, int CurrentPlayerId = 0, IReadOnlyList<PlayerScore>? Scoreboard = null);

  public record WinnerResponse(int PlayerId, string Name, int Score);
}
