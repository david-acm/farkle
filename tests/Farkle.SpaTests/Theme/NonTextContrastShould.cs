using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using MudBlazor.Utilities;
using Xunit;

namespace Farkle.SpaTests.Theme;

// Issue #150 — non-text contrast (WCAG 1.4.11, AA).
//
// UI elements that convey meaning through a border/outline must clear 3:1
// against the colour they sit on. These read the literals from the component
// CSS so the guard tracks the source: the dashed "Set Aside" drop-zone border
// (which marks the drop target) and the die's outline.
public class NonTextContrastShould
{
  private const double NonText = 3.0; // WCAG 1.4.11

  [Fact]
  public void KeepTheSetAsideDropZoneBorderVisible()
  {
    var css = ReadComponentCss("DragabbleDice.razor.css");

    var border = Hex(css, @"border:\s*[\d.]+px\s+dashed\s+(#[0-9A-Fa-f]{6})");
    var zoneBackground = Hex(css, @"\.zone\b[^{}]*\{[^}]*background:\s*(#[0-9A-Fa-f]{6})");

    ContrastMath.Ratio(border, zoneBackground)
      .Should().BeGreaterThanOrEqualTo(NonText,
        "the dashed drop-target border must be distinguishable from the zone fill");
  }

  [Fact]
  public void KeepTheDieOutlineVisible()
  {
    var css = ReadComponentCss("Die.razor.css");

    var border = Hex(css, @"border:\s*(#[0-9A-Fa-f]{6})\s+[\d.]+px\s+solid");
    var face = Hex(css, @"background-color:\s*(#[0-9A-Fa-f]{6})");

    ContrastMath.Ratio(border, face)
      .Should().BeGreaterThanOrEqualTo(NonText,
        "the die outline must be distinguishable from the die face");
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
