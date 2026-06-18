namespace Blazor.Dice;

public interface IRotationCalculator
{
  /// <summary>
  /// The 3D rotation (degrees) that shows <paramref name="dieValue"/> face-up.
  /// When <paramref name="randomSpin"/> is true the rotation also includes random full turns,
  /// so a transition to it reads as a roll rather than a snap to the face.
  /// </summary>
  DieRotation CalculateFor(DieValue dieValue, bool randomSpin);
}
