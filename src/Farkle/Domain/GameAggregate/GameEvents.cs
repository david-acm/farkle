using System.Collections.Immutable;
using Ardalis.SmartEnum;
using Eventuous;
using Farkle.Application;

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

internal interface IRandom
{
  int Next(int minValue, int maxValue);
}

internal static class GameEvents
{
  internal static class V1
  {
    [EventType("V1.GameStarted")]
    internal record GameStarted(int Id);

    [EventType("V1.PlayerJoined")]
    internal record PlayerJoined(int Id, string Name);

    [EventType("V1.DiceRolled")]
    internal record DiceRolled(int PlayerId, int[] Dice, Score TurnScore);

    [EventType("V1.DiceKept")]
    internal record DiceKept(int PlayerId, int[] Dice, int[] TableCenter, int NewTurnScore);

    [EventType("V1.TurnPassed")]
    internal record TurnPassed(int PlayerId, ImmutableArray<Player> PlayerOrder, int GameScore);

    [EventType("V1.PlayedOutOfTurn")]
    internal record PlayedOutOfTurn(int TriedToPlay, int ExpectedPlayer) : IErrorEvent;

    [EventType("V1.RolledTwice")]
    internal record RolledTwice(int Player) : IErrorEvent;

    [EventType("V1.PassedWithoutRolling")]
    internal record PassedWithoutRolling(int PlayerId) : IErrorEvent;
  }

  internal static class V2
  {
    [EventType("V2.DiceRolled")]
    internal record DiceRolled(int PlayerId, int[] Dice, Score TurnScore, GameStage Stage);

    [EventType("V2.DiceKept")]
    internal record DiceKept(int PlayerId, int[] Dice, int[] TableCenter, int NewTurnScore, GameStage Stage);
  }
}

internal interface IErrorEvent
{
}

internal sealed class DieValue : SmartEnum<DieValue, int>
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
