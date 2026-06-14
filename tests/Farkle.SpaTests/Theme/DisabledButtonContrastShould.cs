using FluentAssertions;
using MudBlazor;
using MudBlazor.Utilities;
using WebApp.Client.Layout;
using Xunit;

namespace Farkle.SpaTests.Theme;

// Issue #146 — disabled buttons have poor contrast on the dark theme.
//
// MudButton's disabled state paints its label/icon with the palette's
// `ActionDisabled` colour over an `ActionDisabledBackground`. The app ships a
// DARK palette, but if those two values are left unset MudBlazor falls back to
// its LIGHT-theme defaults (near-black, semi-transparent), which vanish against
// the near-black page background.
//
// These guards composite the (possibly semi-transparent) disabled colours over
// the opaque page background and assert the result is actually legible, so the
// dark-theme disabled colours can't silently regress to the light defaults.
public class DisabledButtonContrastShould
{
  private static readonly PaletteLight Palette = (PaletteLight)FarkleTheme.Theme.PaletteLight;

  [Fact]
  public void KeepDisabledLabelLegible_AgainstItsDisabledBackground()
  {
    var pageBackground = Palette.Background;
    var disabledBackground = CompositeOver(Palette.ActionDisabledBackground, pageBackground);
    var disabledText = CompositeOver(Palette.ActionDisabled, disabledBackground);

    ContrastRatio(disabledText, disabledBackground)
      .Should().BeGreaterThanOrEqualTo(4.5,
        "a disabled button's label/icon must stay readable on the dark theme");
  }

  [Fact]
  public void KeepDisabledButtonVisible_AgainstThePageBackground()
  {
    var pageBackground = Palette.Background;
    var disabledBackground = CompositeOver(Palette.ActionDisabledBackground, pageBackground);

    ContrastRatio(disabledBackground, pageBackground)
      .Should().BeGreaterThanOrEqualTo(1.2,
        "a disabled button must still be distinguishable from the page background");
  }

  // Alpha-composite `foreground` (which may be semi-transparent) over an opaque
  // `background`, returning an opaque colour.
  private static MudColor CompositeOver(MudColor foreground, MudColor background)
  {
    var a = foreground.A / 255.0;
    var r = (foreground.R * a) + (background.R * (1 - a));
    var g = (foreground.G * a) + (background.G * (1 - a));
    var b = (foreground.B * a) + (background.B * (1 - a));
    return new MudColor((byte)Math.Round(r), (byte)Math.Round(g), (byte)Math.Round(b), (byte)255);
  }

  private static double ContrastRatio(MudColor a, MudColor b)
  {
    var la = RelativeLuminance(a);
    var lb = RelativeLuminance(b);
    var (lighter, darker) = la >= lb ? (la, lb) : (lb, la);
    return (lighter + 0.05) / (darker + 0.05);
  }

  // WCAG 2.x relative luminance.
  private static double RelativeLuminance(MudColor c)
  {
    double Channel(double v)
    {
      v /= 255.0;
      return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
  }
}
