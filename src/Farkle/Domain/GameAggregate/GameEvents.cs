using System.Collections.Immutable;
using Ardalis.SmartEnum;
using Farkle.Application;
using Farkle.SharedKernel.Turns;

namespace Farkle.Domain.GameAggregate;

internal record Dice(IEnumerable<DieValue> DiceValues)
{
  public static Dice FromNewRoll(IRandom randomizer, int diceToRoll)
  {
    var dice = new List<DieValue>();
    for (var i = 1; i <= diceToRoll; i++)
    {
      dice.Add(DieValue.FromValue(randomizer.Next(1, 7)));
    }

    return new Dice(dice);
  }

  public static Dice FromValues(IEnumerable<int> values)
  {
    var valueList = values.ToList();
    if (valueList.Count > 6)
    {
      throw new ArgumentOutOfRangeException($"Can't Roll more than 6 dice. Found: {valueList}");
    }

    return new Dice(valueList.ToDiceValues());
  }
}

// DI-registered seam for the dice source (#93). Public because Wolverine's generated handler code
// (a separate assembly) resolves it as a constructor/method dependency. The default implementation
// (DefaultRandomProvider) is registered in the Farkle module; tests substitute a deterministic one.
public interface IRandom
{
  int Next(int minValue, int maxValue);
}

// Events are public (ADR 0004): Marten discovers the GameState.Apply(<Event>) overloads and the
// slice handlers return them. Marten registers event types by CLR type (greenfield data, ADR 0002),
// so the Eventuous [EventType] attributes are gone. Never modify a V1 schema — add a V2.
public static class GameEvents
{
  public static class V1
  {
    public record GameStarted(int Id);

    public record PlayerJoined(int Id, string Name);

    public record GamePlayStarted(int StartedByPlayerId);

    public record DiceRolled(int PlayerId, int[] Dice, Score TurnScore);

    public record DiceKept(int PlayerId, int[] Dice, int[] TableCenter, int NewTurnScore);

    // #159 — set aside / put back: a transient, non-scoring selection of which rolled dice the
    // player intends to keep. First-class so they can be persisted and broadcast to spectators.
    public record DiceSetAside(int PlayerId, int Die);

    public record DiceReturned(int PlayerId, int Die);

    public record DieNotAvailableToSetAside(int PlayerId, int Die) : IErrorEvent;

    public record DieNotSetAside(int PlayerId, int Die) : IErrorEvent;

    public record TurnPassed(int PlayerId, ImmutableArray<Player> PlayerOrder, int GameScore);

    public record PlayedOutOfTurn(int TriedToPlay, int ExpectedPlayer) : IErrorEvent;

    public record RolledTwice(int Player) : IErrorEvent;

    public record PassedWithoutRolling(int PlayerId) : IErrorEvent;

    public record OnlyHostCanStartGame(int PlayerId, int HostId) : IErrorEvent;

    public record NotEnoughPlayers(int PlayerCount, int Minimum) : IErrorEvent;

    public record GameAlreadyInPlay(GameStage Stage) : IErrorEvent;

    public record GameWon(int PlayerId, int Score);
  }

  public static class V2
  {
    // V2 of PlayerJoined adds the player's identity Color (assigned from PlayerColors by join
    // order). V1.PlayerJoined is left untouched; GameState handles both, deriving the colour from
    // the id for V1 so older streams still render a consistent colour.
    public record PlayerJoined(int Id, string Name, string Color);

    public record DiceRolled(int PlayerId, int[] Dice, Score TurnScore, GameStage Stage);

    public record DiceKept(int PlayerId, int[] Dice, int[] TableCenter, int NewTurnScore, GameStage Stage);
  }
}

public interface IErrorEvent
{
}

public sealed class DieValue : SmartEnum<DieValue, int>
{
  public static readonly DieValue One = new("⚀", 1);

  public static readonly DieValue Two = new("⚁", 2);

  public static readonly DieValue Three = new("⚂", 3);

  public static readonly DieValue Four = new("⚃", 4);

  public static readonly DieValue Five = new("⚄", 5);

  public static readonly DieValue Six = new("⚅", 6);

  private DieValue(string name, int value) : base(name,
    value)
  {
  }
}
