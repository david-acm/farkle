namespace Farkle.Contracts;

public static class HttpResponses
{
  public record JoinPlayerResponse(int Id);
  
  public record PlayerScore(int PlayerId, int Score);
  
  public record KeepDiceResponse(int Id, int TurnScore);
  
  public record RollDiceResponse(int Id, int[] DiceValues);
  
  public record StartGameResponse(int Id);

  public record PassTurnResponse(int GameId, int PlayerId, int NewScore, WinnerResponse? Winner);

  public record WinnerResponse(int PlayerId, string Name, int Score);
}
