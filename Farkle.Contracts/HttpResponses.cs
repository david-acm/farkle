namespace Farkle.Contracts;

public static class HttpResponses
{
  public record JoinPlayerResponse(int Id);
  
  public record PlayerScore(int PlayerId, int Score);
  
  public record KeepDiceResponse(int Id, List<PlayerScore> Score);
  
  public record RollDiceResponse(int Id, int[] Dice);
  
  public record StartGameResponse(int Id);
}
