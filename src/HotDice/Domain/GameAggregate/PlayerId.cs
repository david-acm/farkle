namespace HotDice.Domain.GameAggregate;

// A player's id within a game. This is a value object, not a command input: it appears on the
// events, on GameState (GameScoreFor) and on every slice's command, so it belongs with the other
// value objects (GameId, Score, DieValue) rather than in any one slice.
public record PlayerId(int Id)
{
  public static implicit operator int(PlayerId id)
  {
    return id.Id;
  }

  public static implicit operator PlayerId(int id)
  {
    return new PlayerId(id);
  }
}
