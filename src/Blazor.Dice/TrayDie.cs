namespace Blazor.Dice;

public class TrayDie(int index, DieValue value, string identifier)
{
  public int       Index      { get; init; } = index;
  public DieValue Value      { get; set; }  = value;
  public string    Identifier { get; set; }  = identifier;

  // True only for dice that were just rolled — they play the roll (spin) animation.
  // Set false once a die is moved between zones so re-selecting a die never looks like a re-roll.
  public bool      Animate    { get; set; }  = true;
}
