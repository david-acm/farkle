namespace Farkle.Contracts;

public static class HttpResponses
{
  public record JoinPlayerResponse(
    int Id,
    int CurrentPlayerId,
    int HostPlayerId = 0,
    string Stage = "",
    IReadOnlyList<LobbyPlayer>? Roster = null);

  public record LobbyPlayer(int PlayerId, string Name, string Color = "");

  public record LobbyStateResponse(
    int GameId,
    string Stage,
    IReadOnlyList<LobbyPlayer> Roster,
    int HostPlayerId,
    int CurrentPlayerId,
    // #244 — server-assigned turn ordinal (0 in the lobby, 1 once play begins); telemetry entity key.
    int TurnNumber = 0);

  public record PlayerScore(int PlayerId, string Name, int Score, string Color = "");
  
  public record KeepDiceResponse(int Id, int TurnScore);

  // #159 — the in-turn player's current transient set-aside selection after the change.
  public record SetAsideResponse(int Id, IReadOnlyList<int> DiceSetAside);
  
  public record RollDiceResponse(int Id, int[] DiceValues);
  
  public record StartGameResponse(int Id);

  public record PassTurnResponse(int GameId, int PlayerId, int NewScore, WinnerResponse? Winner, int CurrentPlayerId = 0, IReadOnlyList<PlayerScore>? Scoreboard = null, int TurnNumber = 0);

  public record WinnerResponse(int PlayerId, string Name, int Score);

  // Full game-state snapshot for restoring a player's view on refresh / reconnect.
  // Identity-free: "who am I" is supplied client-side (sessionStorage), not by the server.
  public record GameStateResponse(
    int GameId,
    string Stage,
    int CurrentPlayerId,
    int HostPlayerId,
    int TurnScore,
    IReadOnlyList<PlayerScore> Scoreboard,
    WinnerResponse? Winner,
    IReadOnlyList<int> TableCenter,
    IReadOnlyList<int> DiceKept,
    // #159 — the in-turn player's transient set-aside selection (overlays TableCenter),
    // so spectators can render the live keep selection and a refresh restores it.
    IReadOnlyList<int> DiceSetAside,
    // #244 — server-assigned turn ordinal; telemetry entity key carried to spectators on broadcasts.
    int TurnNumber = 0);
}
