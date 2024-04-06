namespace Farkle.Domain.GameAggregate;

public static class EventExtensions
{
  public static int[] ToPrimitiveArray(this IEnumerable<DiceValue> values)
  {
    return values.Select(v => v.Value).ToArray();
  }

  public static IEnumerable<DiceValue> ToDiceValues(this IEnumerable<int> values)
  {
    return values.Select(DiceValue.FromValue);
  }
}
