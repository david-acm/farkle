using MudBlazor;

namespace WebApp.Client.Layout;

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
    },
    Typography = new Typography
    {
      H3 = new H3Typography { FontFamily = ["Press Start 2P", "monospace"] },
      H5 = new H5Typography { FontFamily = ["Press Start 2P", "monospace"] },
      H6 = new H6Typography { FontFamily = ["Press Start 2P", "monospace"] },
    }
  };
}
