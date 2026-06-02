using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Farkle.StoryboardTests;

/// <summary>
/// Multi-viewport storyboard capture (issue #98). For each common screen size the test
/// drives the host's view through the opening of a game and screenshots the page
/// immediately before each user interaction:
///
///   01-landing → 02-lobby → 03-roll → 04-drag → 05-keep → 06-pass
///
/// Frames are written to <c>test-results/storyboard/{step}-{viewport}.png</c> so they
/// sort in interaction order and group by viewport. The backend is the in-memory
/// <see cref="InMemoryAggregateStore"/> (no Testcontainers); a deterministic dice source
/// guarantees a scoring die so the keep/pass stages render every time.
/// </summary>
[Collection(StoryboardCollection.Name)]
public class StoryboardShould(StoryboardFixture fixture)
{
  private const int WasmTimeoutMs = 120_000;

  private static readonly string[] Steps =
    ["01-landing", "02-lobby", "03-roll", "04-drag", "05-keep", "06-pass"];

  private static string StoryboardDir =>
    Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
      "test-results", "storyboard"));

  public static IEnumerable<object[]> Viewports =>
  [
    ["mobile", 390, 844],   // common phone portrait
    ["medium", 1280, 800],  // medium desktop / laptop
    ["large", 1920, 1080],  // large desktop
  ];

  [Theory]
  [MemberData(nameof(Viewports))]
  public async Task CaptureOpeningStoryboard(string viewport, int width, int height)
  {
    Directory.CreateDirectory(StoryboardDir);

    var context = await fixture.NewContextAsync(width, height);
    var page    = await context.NewPageAsync();
    try
    {
      // 1. Landing — before starting a new game.
      await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.Commit });
      await page.WaitForSelectorAsync("[data-testid='start-new-game']", new() { Timeout = WasmTimeoutMs });
      await CaptureAsync(page, "01-landing", viewport);

      // Host starts a new game and auto-joins via the ?name= query param.
      await page.Locator("[placeholder='Your name']").First.FillAsync("Alice");
      await page.WaitForSelectorAsync("[data-testid='start-new-game']:not([disabled])", new() { Timeout = WasmTimeoutMs });
      await page.ClickAsync("[data-testid='start-new-game']");
      await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
      var gameId = int.Parse(Regex.Match(page.Url, @"/games/(\d+)").Groups[1].Value);
      await page.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 15_000 });

      // 2. Lobby — add a second player so the host can begin, then capture before starting.
      await AddSecondPlayerAsync(gameId, "Bob");
      await WaitForStartEnabledAsync(page);
      await CaptureAsync(page, "02-lobby", viewport);

      // Begin the game; the host (Alice) is in turn.
      await page.ClickAsync("[data-testid='start-game-button']");
      await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 15_000 });

      // 3. Roll — before rolling the dice.
      await CaptureAsync(page, "03-roll", viewport);
      await page.ClickAsync("button:has-text('Roll')");
      await page.WaitForSelectorAsync(".die-container", new() { Timeout = 15_000 });

      // 4. Drag — before dragging a scoring die into the set-aside zone.
      await CaptureAsync(page, "04-drag", viewport);
      await DragDieAsync(page, 0);
      await page.Locator("[identifier='SetAside']").Locator(".mud-drop-item").First
        .WaitForAsync(new() { Timeout = 10_000 });

      // 5. Keep — before committing the set-aside dice.
      await CaptureAsync(page, "05-keep", viewport);
      await page.ClickAsync("button:has-text('Set Dice Aside')");
      await page.WaitForTimeoutAsync(300);

      // 6. Pass — before passing the turn.
      await CaptureAsync(page, "06-pass", viewport);
    }
    finally
    {
      await context.CloseAsync();
    }

    foreach (var step in Steps)
      File.Exists(Path.Join(StoryboardDir, $"{step}-{viewport}.png"))
        .Should().BeTrue($"frame {step}-{viewport}.png should have been captured");
  }

  private static Task CaptureAsync(IPage page, string step, string viewport) =>
    page.ScreenshotAsync(new()
    {
      Path     = Path.Join(StoryboardDir, $"{step}-{viewport}.png"),
      FullPage = true,
    });

  private async Task AddSecondPlayerAsync(int gameId, string name)
  {
    using var client = fixture.CreateApiClient();
    var response = await client.PostAsJsonAsync($"/api/games/{gameId}/players",
      new { gameId, playerName = name });
    response.EnsureSuccessStatusCode();
  }

  // The host's Start button enables once the roster has two players. The second player
  // was added out-of-band via the API, so reload to re-read the roster from the server
  // snapshot (GET /api/games/{id}) — deterministic, unlike waiting on the SignalR
  // PlayerJoined push which may not arrive promptly in CI.
  private static async Task WaitForStartEnabledAsync(IPage page)
  {
    await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Commit });
    await page.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
      new() { Timeout = WasmTimeoutMs });
  }

  // MudBlazor's MudDropZone uses HTML5 drag events, which Playwright's mouse-based
  // DragToAsync doesn't reliably trigger in headless Chromium — dispatch them via JS.
  private static Task DragDieAsync(IPage page, int index) =>
    page.EvaluateAsync($@"() => {{
      const dice   = document.querySelectorAll('.mud-drop-zone')[0]
                             .querySelectorAll('.mud-drop-item-draggable');
      const source = dice[{index}];
      const target = document.querySelectorAll('.mud-drop-zone')[1];
      const dt     = new DataTransfer();
      source.dispatchEvent(new DragEvent('dragstart', {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
      target.dispatchEvent(new DragEvent('dragenter', {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
      target.dispatchEvent(new DragEvent('dragover',  {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
      target.dispatchEvent(new DragEvent('drop',      {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
      source.dispatchEvent(new DragEvent('dragend',   {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
    }}");
}
