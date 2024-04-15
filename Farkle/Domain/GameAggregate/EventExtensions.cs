namespace Farkle.Domain.GameAggregate;

internal static class EventExtensions
{
  public static int[] ToPrimitiveArray(this IEnumerable<DieValue> values)
  {
    return values.Select(v => v.Value).ToArray();
  }

  public static IEnumerable<DieValue> ToDiceValues(this IEnumerable<int> values)
  {
    return values.Select(DieValue.FromValue);
  }
}
