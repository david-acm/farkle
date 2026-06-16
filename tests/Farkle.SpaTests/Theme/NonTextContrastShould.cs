using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using MudBlazor.Utilities;
using Xunit;

namespace Farkle.SpaTests.Theme;

// Issue #150 — non-text contrast (WCAG 1.4.11, AA).
//
// UI elements that convey meaning through a border/outline or colour must clear 3:1
// against the colour they sit on. These read the literals from the component CSS so the
// guard tracks the source: the selected (tapped) die's dark pips on its yellow face
// (#182, the selection cue) and the die's outline.
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
    // The dice scoped CSS lives in the Blazor.Dice library; other game components
    // keep theirs under WebApp.Client. Search both relative to the repo root.
    string[] roots =
    [
      Path.Join("src", "Blazor.Dice"),
      Path.Join("src", "WebApp.Client", "Pages", "Game", "Components"),
    ];

    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
      foreach (var root in roots)
      {
        var candidate = Path.Join(dir.FullName, root, fileName);
        if (File.Exists(candidate)) return File.ReadAllText(candidate);
      }
      dir = dir.Parent;
    }

    throw new FileNotFoundException($"Could not locate {fileName} from {AppContext.BaseDirectory}");
  }
}
