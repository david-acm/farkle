namespace Blazor.Dice;

// A die shown in a <see cref="DiceTray"/>: its value, whether it's selected, and whether it
// should play the one-shot roll (spin) animation. State is get-only and mutated through
// intent-revealing methods (Select / Deselect / DisableAnimation) so callers read by meaning
// instead of poking setters. Construct via the fluent factories (Unselected / Selected).
public sealed class TrayDie
{
  private TrayDie(int index, DieValue value, bool selected)
  {
    Index      = index;
    Value      = value;
    IsSelected = selected;
  }

  public int      Index { get; }
  public DieValue Value { get; }

  public bool IsSelected { get; private set; }

  // True only for a freshly rolled die — it plays the roll animation once. Cleared
  // (DisableAnimation) as soon as a die is moved/settled so re-selecting never looks like a re-roll.
  public bool IsAnimated { get; private set; } = true;

  // Fluent factories — construction reads by intent (animated by default; chain DisableAnimation()).
  public static TrayDie Unselected(int index, DieValue value) => new(index, value, selected: false);
  public static TrayDie Selected(int index, DieValue value)   => new(index, value, selected: true);

  public TrayDie Select()           { IsSelected = true;  return this; }
  public TrayDie Deselect()         { IsSelected = false; return this; }
  public TrayDie DisableAnimation() { IsAnimated = false; return this; }
}
