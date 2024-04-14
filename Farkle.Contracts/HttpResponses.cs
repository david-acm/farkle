namespace Farkle.Contracts;

public static class HttpResponses
{
  public record JoinPlayerResponse(int Id);
  public record KeepDiceResponse(int Id);
  
  public record RollDiceResponse(int Id);
  
  public record StartGameResponse(int Id);
}
