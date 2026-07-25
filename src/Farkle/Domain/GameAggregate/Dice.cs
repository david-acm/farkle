namespace Farkle.Domain.GameAggregate;

// A set of rolled dice. A value object, not an event — the RollDice slice builds one from the
// IRandom seam and hands it to the decider, which turns it into the DiceRolled event.
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
