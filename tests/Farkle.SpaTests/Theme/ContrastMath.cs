using MudBlazor.Utilities;

namespace Farkle.SpaTests.Theme;

/// <summary>
/// WCAG 2.x colour-contrast maths shared by the theme accessibility guards.
/// </summary>
public static class ContrastMath
{
  /// <summary>
  /// Contrast ratio between two colours. Any alpha on either colour is first
  /// composited over <paramref name="canvas"/> (an opaque backdrop) so a
  /// semi-transparent token is measured as it actually renders.
  /// </summary>
  public static double Ratio(MudColor foreground, MudColor background, MudColor canvas)
  {
    var bg = CompositeOver(background, canvas);
    var fg = CompositeOver(foreground, bg);
    return Ratio(fg, bg);
  }

  /// <summary>Contrast ratio between two already-opaque colours.</summary>
  public static double Ratio(MudColor a, MudColor b)
  {
    var la = RelativeLuminance(a);
    var lb = RelativeLuminance(b);
    var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
    return (lighter + 0.05) / (darker + 0.05);
  }

  /// <summary>
  /// Alpha-composite <paramref name="foreground"/> (possibly translucent) over
  /// an opaque <paramref name="background"/>, returning an opaque colour.
  /// </summary>
  public static MudColor CompositeOver(MudColor foreground, MudColor background)
  {
    var a = foreground.A / 255.0;
    if (a >= 1.0) return foreground;

    var r = (foreground.R * a) + (background.R * (1 - a));
    var g = (foreground.G * a) + (background.G * (1 - a));
    var b = (foreground.B * a) + (background.B * (1 - a));
    return new MudColor((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(b), (byte)255);
  }

  // WCAG 2.x relative luminance.
  public static double RelativeLuminance(MudColor c)
  {
    double Channel(double v)
    {
      v /= 255.0;
      return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
  }
}
