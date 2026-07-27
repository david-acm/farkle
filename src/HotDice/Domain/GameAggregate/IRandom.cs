namespace HotDice.Domain.GameAggregate;

// DI-registered seam for the dice source (#93). Public because Wolverine's generated handler code
// (a separate assembly) resolves it as a constructor/method dependency. The default implementation
// (DefaultRandomProvider) is registered in the HotDice module; tests substitute a deterministic one.
public interface IRandom
{
  int Next(int minValue, int maxValue);
}
