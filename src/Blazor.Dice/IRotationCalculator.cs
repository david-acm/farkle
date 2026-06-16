
namespace Blazor.Dice;

public interface IRotationCalculator
{
  (int, int, int) CalculateFor(DieValue dieValue, bool randomSpin = false);
}
