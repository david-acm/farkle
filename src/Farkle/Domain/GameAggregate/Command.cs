namespace Farkle.Domain.GameAggregate;

// Commands are the write-side's contract. Each slice's Wolverine.HTTP endpoint constructs its command
// inline and passes it to the pure decider (`Decide(command, state) -> events`); the endpoint loads the
// aggregate via [WriteAggregate(FromMethod = nameof(StreamId))] where `StreamId(int)` yields the
// "game-{code}" stream key (ADR 0004, as-shipped Option C). Commands are not dispatched as messages.
// StartGame has no existing stream, so it goes through IGameCreator (StartStream) instead.
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
