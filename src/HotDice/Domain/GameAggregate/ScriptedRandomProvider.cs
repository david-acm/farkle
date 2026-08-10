namespace HotDice.Domain.GameAggregate;

// Deterministic dice source, selected in the host only when Dice:Scripted is configured (#339).
// Dice are rolled via IRandom.Next(1, 7); always returning the minimum yields six 1s, so every roll
// contains a scoring die. That lets the black-box device happy path reliably drive set-aside → keep
// (and #345's post-deploy check) instead of leaving Keep best-effort. Production never sets the flag
// and uses DefaultRandomProvider; this mirrors the storyboard's test-side ScriptedRandom.
internal sealed class ScriptedRandomProvider : IRandom
{
  public int Next(int minValue, int maxValue) => minValue;
}
