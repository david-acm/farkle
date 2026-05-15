using Ardalis.SmartEnum;

namespace WebApp.Client.Pages.Game.Components;

public sealed class DieValue : SmartEnum<DieValue, int>
{
  public static readonly DieValue None = new("n", 0);
  public static readonly DieValue One  = new("⚀", 1);

  public static readonly DieValue Two = new("⚁", 2);

  public static readonly DieValue Three = new("⚂", 3);

  public static readonly DieValue Four = new("⚃", 4);

  public static readonly DieValue Five = new("⚄", 5);

  public static readonly DieValue Six = new("⚅", 6);

  private DieValue(string name, int value) : base(name,
    value)
  {
  }
}
