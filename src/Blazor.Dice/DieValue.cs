using Ardalis.SmartEnum;

namespace Blazor.Dice;

public sealed class DieValue : SmartEnum<DieValue, int>
{
  public static readonly DieValue None  = new("None",  0, "");
  public static readonly DieValue One   = new("One",   1, "⚀");
  public static readonly DieValue Two   = new("Two",   2, "⚁");
  public static readonly DieValue Three = new("Three", 3, "⚂");
  public static readonly DieValue Four  = new("Four",  4, "⚃");
  public static readonly DieValue Five  = new("Five",  5, "⚄");
  public static readonly DieValue Six   = new("Six",   6, "⚅");

  private DieValue(string name, int value, string pip) : base(name, value) => Pip = pip;

  /// <summary>The Unicode die-face glyph for this value (e.g. "⚄" for Five); empty for None.</summary>
  public string Pip { get; }
}
