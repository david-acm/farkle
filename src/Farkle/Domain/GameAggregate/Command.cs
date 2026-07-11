namespace Farkle.Domain.GameAggregate;

// Commands are public — they are the Wolverine messages (the write-side's contract). The computed
// `Id` on each stream-mutating command yields the "game-{code}" stream key so Wolverine's
// [AggregateHandler] resolves the aggregate by convention (Option A, ADR 0004). StartGame is handled
// by a StartStream handler, so it needs no Id.
public static class Command
{
  public record KeepDice(GameId GameId, PlayerId PlayerId, IEnumerable<DieValue> DiceValues)
  {
    public string Id => $"game-{GameId.Id}";
  }

  public record SetDiceAside(GameId GameId, PlayerId PlayerId, DieValue Die)
  {
    public string Id => $"game-{GameId.Id}";
  }

  public record ReturnDice(GameId GameId, PlayerId PlayerId, DieValue Die)
  {
    public string Id => $"game-{GameId.Id}";
  }

  public record StartGame(GameId GameId)
  {
    public static implicit operator int(StartGame startGame)
    {
      return startGame.GameId.Id;
    }
  }

  public record JoinPlayer(int GameId, string Name)
  {
    public string Id => $"game-{GameId}";
  }

  public record BeginGame(GameId GameId, PlayerId PlayerId)
  {
    public string Id => $"game-{GameId.Id}";
  }

  public record RollDice(GameId GameId, PlayerId PlayerId)
  {
    public string Id => $"game-{GameId.Id}";
  }

  public record PassTurn(GameId GameId, PlayerId PlayerId)
  {
    public string Id => $"game-{GameId.Id}";
  }

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
}
