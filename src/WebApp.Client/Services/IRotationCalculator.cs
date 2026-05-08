using WebApp.Client.Pages.Game.Components;

namespace WebApp.Client.Services;

public interface IRotationCalculator
{
  (int, int, int) CalculateFor(DieValue dieValue, bool randomSpin = false);
}
