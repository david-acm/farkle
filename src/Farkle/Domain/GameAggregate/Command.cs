namespace Farkle.Domain.GameAggregate;

internal static class Command
{
  internal record KeepDice(GameId GameId, PlayerId PlayerId, IEnumerable<DieValue> DiceValues);

  internal record StartGame(GameId GameId)
  {
    public static implicit operator int(StartGame startGame)
    {
      return startGame.GameId.Id;
    }
  }

  internal record JoinPlayer(int GameId, PlayerId Id, string Name);

  internal record RollDice(GameId GameId, PlayerId PlayerId);

  internal record PassTurn(GameId GameId, PlayerId PlayerId);

  internal record PlayerId(int Id)
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
}
