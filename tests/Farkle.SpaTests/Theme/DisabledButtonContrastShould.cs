using FluentAssertions;
using MudBlazor;
using Farkle.Ui.Layout;
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
    var disabledBackground = ContrastMath.CompositeOver(Palette.ActionDisabledBackground, pageBackground);
    var disabledText = ContrastMath.CompositeOver(Palette.ActionDisabled, disabledBackground);

    ContrastMath.Ratio(disabledText, disabledBackground)
      .Should().BeGreaterThanOrEqualTo(4.5,
        "a disabled button's label/icon must stay readable on the dark theme");
  }

  [Fact]
  public void KeepDisabledButtonVisible_AgainstThePageBackground()
  {
    var pageBackground = Palette.Background;
    var disabledBackground = ContrastMath.CompositeOver(Palette.ActionDisabledBackground, pageBackground);

    ContrastMath.Ratio(disabledBackground, pageBackground)
      .Should().BeGreaterThanOrEqualTo(1.2,
        "a disabled button must still be distinguishable from the page background");
  }
}
