using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace HotDice.SpaTests.Architecture;

// Issue #150 — icon-only buttons must expose an accessible name.
//
// A MudIconButton renders a <button> whose only content is an SVG icon, so
// without an `aria-label` (or `Title`) a screen reader announces it as just
// "button" (WCAG 4.1.2 Name, Role, Value). This scans the component source so
// the rule covers every icon button, current and future.
public class IconButtonAccessibilityShould
{
  [Fact]
  public void GiveEveryIconButtonAnAccessibleName()
  {
    var clientRoot = LocateClientRoot();

    var offenders = Directory
      .EnumerateFiles(clientRoot, "*.razor", SearchOption.AllDirectories)
      .SelectMany(file => IconButtonTags(File.ReadAllText(file))
        .Where(tag => !HasAccessibleName(tag))
        .Select(tag => $"{Path.GetFileName(file)}: {Collapse(tag)}"))
      .ToList();

    offenders.Should().BeEmpty(
      "every icon-only MudIconButton must set aria-label or Title so assistive " +
      "technology can announce it");
  }

  // Each `<MudIconButton …>` element (may span multiple lines). Stop at the
  // real tag close — `/>` for the usual self-closing form, or `</MudIconButton>`
  // for the rarer child-content form — so a lambda's `=>` inside an attribute
  // (e.g. OnClick) doesn't truncate the match before its aria-label/Title.
  private static IEnumerable<string> IconButtonTags(string razor) =>
    Regex.Matches(razor, @"<MudIconButton\b[\s\S]*?(?:/>|</MudIconButton>)").Select(m => m.Value);

  private static bool HasAccessibleName(string tag) =>
    tag.Contains("aria-label", StringComparison.OrdinalIgnoreCase) ||
    Regex.IsMatch(tag, @"\bTitle\s*=");

  private static string Collapse(string tag) =>
    Regex.Replace(tag, @"\s+", " ").Trim();

  private static string LocateClientRoot()
  {
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
      var candidate = Path.Join(dir.FullName, "src", "HotDice.Ui");
      if (Directory.Exists(candidate)) return candidate;
      dir = dir.Parent;
    }

    throw new DirectoryNotFoundException(
      "Could not locate src/HotDice.Ui from " + AppContext.BaseDirectory);
  }
}
