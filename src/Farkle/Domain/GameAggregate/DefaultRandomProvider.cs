namespace Farkle.Domain.GameAggregate;

internal class DefaultRandomProvider : IRandom
{
  public int Next(int minValue, int maxValue)
  {
    return new Random().Next(minValue, maxValue);
  }
}
