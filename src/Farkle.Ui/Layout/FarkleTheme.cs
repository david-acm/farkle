using MudBlazor;

namespace Farkle.Ui.Layout;

/// <summary>
/// The application's MudBlazor theme. Extracted from <c>MainLayout</c> so the
/// palette (in particular its contrast choices) can be asserted in tests.
/// </summary>
public static class FarkleTheme
{
  public static MudTheme Theme { get; } = new()
  {
    PaletteLight = new PaletteLight
    {
      Primary          = "#FFE600",
      PrimaryContrastText = "#0A0A1A",
      PrimaryDarken    = "#CCB800",
      PrimaryLighten   = "#FFF280",
      Secondary        = "#FF2D6B",
      SecondaryContrastText = "#0A0A1A",
      Background       = "#0A0A1A",
      Surface          = "#111128",
      AppbarBackground = "#050510",
      AppbarText       = "#FFFFFF",
      DrawerBackground = "#070718",
      DrawerText       = "#AAAACC",
      TextPrimary      = "#E8E8FF",
      TextSecondary    = "#8888BB",
      TableLines       = "#222244",
      Divider          = "#222244",
      // Dark-theme overrides for tokens that otherwise inherit MudBlazor's
      // near-black light-theme defaults and vanish on the dark background
      // (#146 + follow-up). ActionDisabled* keep disabled buttons legible;
      // ActionDefault keeps default-coloured icons visible; TextDisabled keeps
      // disabled/placeholder text readable while still clearly muted.
      ActionDisabledBackground = "#2A2A45",
      ActionDisabled           = "#9A9AC8",
      ActionDefault            = "#C8C8E8",
      TextDisabled             = "#6E6E95",
    },
    Typography = new Typography
    {
      H3 = new H3Typography { FontFamily = ["Press Start 2P", "monospace"] },
      H5 = new H5Typography { FontFamily = ["Press Start 2P", "monospace"] },
      H6 = new H6Typography { FontFamily = ["Press Start 2P", "monospace"] },
    }
  };
}
