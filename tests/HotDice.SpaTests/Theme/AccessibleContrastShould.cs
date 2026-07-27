using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using MudBlazor;
using MudBlazor.Utilities;
using HotDice.Ui.Layout;
using Xunit;

namespace HotDice.SpaTests.Theme;

// Issue #146 follow-up — extend the objective disabled-button contrast guard to
// the rest of the app's controls so "is this readable on the dark theme?" is
// answered by WCAG maths in CI rather than by eye.
//
// Every case reads the LIVE palette (so it also pins down MudBlazor's defaults
// for tokens the theme leaves unset — those default to light-theme colours that
// are wrong on our dark background) and composites translucent tokens over the
// canvas they render on before measuring.
public class AccessibleContrastShould
{
  private static readonly PaletteLight Palette = (PaletteLight)HotDiceTheme.Theme.PaletteLight;

  private const double AaNormalText = 4.5;   // WCAG AA, normal-size text/icons
  private const double VisibleMuted = 3.0;   // intentionally de-emphasised, but must still be seen

  // --- Foreground/background pairs the app actually renders ---------------
  //
  //  fg token            bg token            min       where it shows
  [Theory]
  // Filled buttons: label/icon over the button fill.
  [InlineData("PrimaryContrastText",   "Primary",          AaNormalText)] // Roll / Keep / Join / Start
  [InlineData("SecondaryContrastText", "Secondary",        AaNormalText)] // Pass Turn
  // Body text over both canvases.
  [InlineData("TextPrimary",   "Background", AaNormalText)]
  [InlineData("TextPrimary",   "Surface",    AaNormalText)]
  [InlineData("TextSecondary", "Background", AaNormalText)]
  [InlineData("TextSecondary", "Surface",    AaNormalText)]
  // App chrome.
  [InlineData("AppbarText",  "AppbarBackground", AaNormalText)]
  [InlineData("DrawerText",  "DrawerBackground", AaNormalText)]
  // Yellow used AS text (titles, scores, game code, scoreboard leader).
  [InlineData("Primary", "Background", AaNormalText)]
  [InlineData("Primary", "Surface",    AaNormalText)]
  // Semantic colours used as text/icons (Text-variant alerts & buttons).
  [InlineData("Success", "Background", AaNormalText)] // "It's your turn!" alert
  [InlineData("Info",    "Background", AaNormalText)] // "Waiting for …" alert
  [InlineData("Error",   "Surface",    AaNormalText)] // ErrorModal close button on the popover
  public void MeetAaContrast(string foreground, string background, double min)
  {
    var fg = Token(foreground);
    var bg = Token(background);

    ContrastMath.Ratio(fg, bg, canvas: Token("Background"))
      .Should().BeGreaterThanOrEqualTo(min,
        $"'{foreground}' on '{background}' must stay legible on the dark theme");
  }

  // --- Muted tokens: must be visible against BOTH canvases ----------------
  // These default (when unset) to MudBlazor's near-black light-theme values,
  // which disappear on the dark background — the same trap as the disabled
  // buttons. They are allowed to be muted, but not invisible.
  [Theory]
  [InlineData("TextDisabled")] // disabled field text / placeholders
  [InlineData("ActionDefault")] // default-coloured icons
  public void StayVisible_AgainstBothCanvases(string token)
  {
    var fg = Token(token);

    foreach (var canvas in new[] { "Background", "Surface" })
    {
      ContrastMath.Ratio(fg, Token(canvas), canvas: Token("Background"))
        .Should().BeGreaterThanOrEqualTo(VisibleMuted,
          $"'{token}' must remain visible on '{canvas}'");
    }
  }

  // --- Component-local colours (P3): the winner banner -------------------
  // The banner hard-codes its text colour over a gradient in Scoreboard.razor.
  // The worst case is the lighter gradient stop, so check the text against BOTH
  // stops. Read the literals from the component so the guard tracks the source.
  [Fact]
  public void KeepWinnerBannerTextReadable_OverItsGradient()
  {
    var razor = File.ReadAllText(LocateComponent("Pages/Game/Components/Scoreboard.razor"));

    var textColor = HexAfter(razor, @"color:\s*(#[0-9A-Fa-f]{6})");
    var stops = Regex.Matches(razor, @"(#[0-9A-Fa-f]{6})\s+\d+%")
      .Select(m => new MudColor(m.Groups[1].Value))
      .ToArray();

    stops.Should().NotBeEmpty("the winner banner should define gradient stops");

    foreach (var stop in stops)
    {
      ContrastMath.Ratio(textColor, stop)
        .Should().BeGreaterThanOrEqualTo(AaNormalText,
          "the winner's name must be readable over every part of the banner gradient");
    }
  }

  private static MudColor Token(string name)
  {
    var prop = typeof(PaletteLight).GetProperty(name)
      ?? throw new ArgumentException($"No palette token named '{name}'");
    return (MudColor)prop.GetValue(Palette)!;
  }

  private static MudColor HexAfter(string source, string pattern)
  {
    var m = Regex.Match(source, pattern);
    m.Success.Should().BeTrue($"expected to find a colour matching /{pattern}/");
    return new MudColor(m.Groups[1].Value);
  }

  private static string LocateComponent(string relativePath)
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
      var candidate = Path.Join(dir.FullName, "src", "HotDice.Ui", relativePath);
      if (File.Exists(candidate)) return candidate;
      dir = dir.Parent;
    }

    throw new FileNotFoundException($"Could not locate {relativePath} from {AppContext.BaseDirectory}");
  }
}
