using System.IO;
using System.Text.RegularExpressions;
using Farkle.Domain.GameAggregate;
using FluentAssertions;
using MudBlazor.Utilities;
using Xunit;

namespace Farkle.SpaTests.Theme;

// Issue #150 — non-text contrast (WCAG 1.4.11, AA).
//
// UI elements that convey meaning through a border/outline or colour must clear 3:1
// against the colour they sit on. These read the literals from the component CSS so the
// guard tracks the source:
// (1) the selected (tapped) die's dark pips on its yellow face (#182, the selection cue), and
// (2) the die outline recolours to the in-turn player's colour (--active-player-color, #170),
//     so EVERY colour in PlayerColors.Palette must stay distinguishable from the die face.
public class NonTextContrastShould
{
  private const double NonText = 3.0; // WCAG 1.4.11

  [Fact]
  public void KeepTheSelectedDiePipsVisible()
  {
    var css = ReadComponentCss("Die.razor.css");

    // A selected die inverts to a yellow face with dark pips — the pips must stay legible.
    var selectedFace = Hex(css, @"\.die-container\.selected[^{}]*\.side\s*\{[^}]*background-color:\s*(#[0-9A-Fa-f]{6})");
    var selectedPip  = Hex(css, @"\.die-container\.selected\s+\.pip\s*\{[^}]*background-color:\s*(#[0-9A-Fa-f]{6})");

    ContrastMath.Ratio(selectedPip, selectedFace)
      .Should().BeGreaterThanOrEqualTo(NonText,
        "a selected die's pips must stay distinguishable from its (inverted) face");
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
    // The dice scoped CSS lives in the Blazor.Dice library; walk up from the test
    // output dir to the repo root and read it from there.
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
      var candidate = Path.Join(dir.FullName, "src", "Blazor.Dice", fileName);
      if (File.Exists(candidate)) return File.ReadAllText(candidate);
    }

    throw new FileNotFoundException($"Could not locate {fileName} from {AppContext.BaseDirectory}");
  }
}
