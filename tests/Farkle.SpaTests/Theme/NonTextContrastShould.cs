using System.IO;
using System.Text.RegularExpressions;
using Farkle.Domain.GameAggregate;
using FluentAssertions;
using MudBlazor.Utilities;
using Xunit;

namespace Farkle.SpaTests.Theme;

// Issue #150 — non-text contrast (WCAG 1.4.11, AA).
//
// UI elements that convey meaning through a border/outline must clear 3:1
// against the colour they sit on. The die outline and the dashed "Set Aside"
// drop-zone border now recolour to the in-turn player's colour
// (--active-player-color), so the guard checks two things from the source:
// (1) the CSS uses the player-colour custom property, and (2) EVERY colour in
// PlayerColors.Palette stays distinguishable from the surface it sits on.
public class NonTextContrastShould
{
  private const double NonText = 3.0; // WCAG 1.4.11

  [Fact]
  public void KeepTheSetAsideDropZoneBorderVisible()
  {
    var css = ReadComponentCss("DragabbleDice.razor.css");

    css.Should().MatchRegex(
      @"\.zone--aside[^{}]*\{[^}]*border:\s*[\d.]+px\s+dashed\s+var\(--active-player-color",
      "the drop-target border must recolour to the in-turn player's colour");

    var zoneBackground = Hex(css, @"\.zone\b[^{}]*\{[^}]*background:\s*(#[0-9A-Fa-f]{6})");

    foreach (var color in PlayerColors.Palette)
      ContrastMath.Ratio(new MudColor(color), zoneBackground)
        .Should().BeGreaterThanOrEqualTo(NonText,
          $"player colour {color} must stay distinguishable as the drop-target border");
  }

  [Fact]
  public void KeepTheDieOutlineVisible()
  {
    var css = ReadComponentCss("Die.razor.css");

    css.Should().MatchRegex(
      @"border:\s*var\(--active-player-color[^)]*\)\s+[\d.]+px\s+solid",
      "the die outline must recolour to the in-turn player's colour");

    var face = Hex(css, @"background-color:\s*(#[0-9A-Fa-f]{6})");

    foreach (var color in PlayerColors.Palette)
      ContrastMath.Ratio(new MudColor(color), face)
        .Should().BeGreaterThanOrEqualTo(NonText,
          $"player colour {color} must stay distinguishable as the die outline");
  }

  private static MudColor Hex(string source, string pattern)
  {
    var m = Regex.Match(source, pattern);
    m.Success.Should().BeTrue($"expected to find a colour matching /{pattern}/");
    return new MudColor(m.Groups[1].Value);
  }

  private static string ReadComponentCss(string fileName)
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
      var candidate = Path.Join(dir.FullName,
        "src", "WebApp.Client", "Pages", "Game", "Components", fileName);
      if (File.Exists(candidate)) return File.ReadAllText(candidate);
      dir = dir.Parent;
    }

    throw new FileNotFoundException($"Could not locate {fileName} from {AppContext.BaseDirectory}");
  }
}
