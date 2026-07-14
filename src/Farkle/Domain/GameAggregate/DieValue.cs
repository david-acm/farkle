using Ardalis.SmartEnum;

namespace Farkle.Domain.GameAggregate;

// The face of a single die. A SmartEnum so the value (1-6) round-trips through Marten via
// SmartEnumValueConverter while the name carries the Unicode pip glyph the UI renders.
public sealed class DieValue : SmartEnum<DieValue, int>
{
  public static readonly DieValue One = new("⚀", 1);

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
