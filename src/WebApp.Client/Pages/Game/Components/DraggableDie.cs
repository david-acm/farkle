namespace WebApp.Client.Pages.Game.Components;

public class DraggableDie(int index, DieValue value, string identifier)
{
  public int       Index      { get; init; } = index;
  public DieValue Value      { get; set; }  = value;
  public string    Identifier { get; set; }  = identifier;
  public bool      IsDragging { get; set; }  = true;
}
