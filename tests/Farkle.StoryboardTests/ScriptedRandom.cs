using Farkle.Domain.GameAggregate;

namespace Farkle.StoryboardTests;

// Deterministic dice source for the storyboard host. Dice are rolled via
// IRandom.Next(1, 7); always returning the minimum yields six 1s — every roll
// therefore contains a scoring die, so the drag/keep/pass stages render
// reproducibly across runs and viewports.
internal sealed class ScriptedRandom : IRandom
{
  public int Next(int minValue, int maxValue) => minValue;
}
