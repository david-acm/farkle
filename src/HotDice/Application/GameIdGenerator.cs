namespace HotDice.Application;

public interface IGameIdGenerator
{
  int Next();
}

public sealed class RandomGameIdGenerator : IGameIdGenerator
{
  // 6-digit codes (100000-999999), easy to share verbally.
  public int Next() => Random.Shared.Next(100_000, 1_000_000);
}
