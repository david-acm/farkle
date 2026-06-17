namespace Blazor.Dice;

// Which tray zone a die occupies. Replaces the former "Rolled"/"SetAside" magic strings (#196).
public enum DiceZone
{
  Rolled,
  SetAside
}

// A die shown in the tray: its value, which zone it sits in, and whether it should play the
// one-shot roll (spin) animation. State is get-only and mutated through intent-revealing methods
// (#196) so callers read by meaning — `die.SetAside()` / `die.DisableAnimation()` — instead of
// poking setters. Construct via the fluent factories (`TrayDie.Rolled` / `TrayDie.SetAside`).
public sealed class TrayDie
{
  private TrayDie(int index, DieValue value, DiceZone zone)
  {
    Index = index;
    Value = value;
    Zone  = zone;
  }

  public int      Index { get; }
  public DieValue Value { get; }
  public DiceZone Zone  { get; private set; }

  // True only for a freshly rolled die — it plays the roll animation once. Cleared
  // (DisableAnimation) as soon as a die is moved/settled so re-selecting never looks like a re-roll.
  public bool IsAnimated { get; private set; } = true;

  public bool IsSelected => Zone == DiceZone.SetAside;

  // Fluent factories — construction reads by intent (animated by default; call DisableAnimation()).
  public static TrayDie Rolled(int index, DieValue value)   => new(index, value, DiceZone.Rolled);
  public static TrayDie SetAside(int index, DieValue value) => new(index, value, DiceZone.SetAside);

  public TrayDie DisableAnimation() { IsAnimated = false; return this; }
  public TrayDie SetAside()         { Zone = DiceZone.SetAside; return this; }
  public TrayDie ReturnToRolled()   { Zone = DiceZone.Rolled;   return this; }
}
