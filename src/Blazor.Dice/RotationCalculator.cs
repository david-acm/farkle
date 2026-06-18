namespace Blazor.Dice;

public class RotationCalculator : IRotationCalculator
{
  public DieRotation CalculateFor(DieValue dieValue, bool randomSpin)
  {
    return dieValue.Value switch
    {
      1 => AddSpinsTo(new DieRotation(105, 0, 15),  randomSpin),
      2 => AddSpinsTo(new DieRotation(15, 165, 0),  randomSpin),
      3 => AddSpinsTo(new DieRotation(15, 255, 0),  randomSpin),
      4 => AddSpinsTo(new DieRotation(15, 345, 0),  randomSpin),
      5 => AddSpinsTo(new DieRotation(15, 75, 0),   randomSpin),
      6 => AddSpinsTo(new DieRotation(285, 0, 345), randomSpin),
      _ => new DieRotation(0, 0, 0)
    };
  }

  private static DieRotation AddSpinsTo(DieRotation rotation, bool randomSpin)
  {
    var rnd         = new Random();
    var spinDegrees = (randomSpin ? rnd.Next(-2, 2) : 0) * 360;
    return new DieRotation(rotation.X + spinDegrees, rotation.Y + spinDegrees, rotation.Z + spinDegrees);
  }
}
