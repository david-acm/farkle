using System.Text.Json;
using System.Text.RegularExpressions;

namespace Farkle.E2eTests;

/// <summary>
/// Shared scaffolding for the Playwright game tests: artifact directories and the
/// landing-page / lobby / drag-die helpers that drive a two-player game through the UI.
/// </summary>
public abstract class GamePlaywrightTest(PlaywrightFixture fixture)
{
    protected const int WasmTimeoutMs = 120_000;

    protected PlaywrightFixture Fixture { get; } = fixture;

    // Pause between notable steps so animations are visible in the recorded video.
    // Override with E2E_STEP_DELAY_MS environment variable (e.g. set to 0 for speed).
    protected static int StepDelayMs =>
        int.TryParse(Environment.GetEnvironmentVariable("E2E_STEP_DELAY_MS"), out var v) ? v : 2_000;

    protected static string VideoDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "videos"));

    protected static string LogDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "logs"));

    protected static string ScreenshotDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "screenshots"));

    // Loads the landing page and waits for WASM hydration (the Start button appears).
    // Full-page navigation (GotoAsync) restarts WASM, giving a fresh BlazorState store
    // with no player-specific data from a previous game session.
    protected async Task GotoLandingAndWaitForWasmAsync(IPage page)
    {
        // WaitUntilState.Commit fires as soon as the server sends response headers,
        // before any CSS or JS is fetched. This bypasses the render-blocking
        // fonts.googleapis.com link that causes GotoAsync to time out waiting for Load.
        await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.Commit });
        await page.WaitForSelectorAsync("[data-testid='start-new-game']",
            new() { Timeout = WasmTimeoutMs });
    }

    // Hosts a new game from the landing page: enters a name, clicks "Start New Game",
    // waits for navigation to /games/{id}, and returns the server-generated id.
    protected async Task<int> StartNewGameFromLandingAsync(IPage page, string playerName)
    {
        await GotoLandingAndWaitForWasmAsync(page);
        // The Start card's name field is the first 'Your name' input.
        await page.Locator("[placeholder='Your name']").First.FillAsync(playerName);
        // Only click once the bind has enabled the button (avoids a default-timeout
        // wait if the value hasn't propagated yet).
        await page.WaitForSelectorAsync("[data-testid='start-new-game']:not([disabled])",
            new() { Timeout = WasmTimeoutMs });
        await page.ClickAsync("[data-testid='start-new-game']");
        await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
        var match = Regex.Match(page.Url, @"/games/(\d+)");
        return int.Parse(match.Groups[1].Value);
    }

    // Joins an existing game from the landing page using its share code.
    protected async Task JoinExistingGameFromLandingAsync(IPage page, string playerName, int gameId)
    {
        await GotoLandingAndWaitForWasmAsync(page);
        // The Join card's name field is the second 'Your name' input.
        await page.Locator("[placeholder='Your name']").Last.FillAsync(playerName);
        await page.FillAsync("[placeholder='Game code']", gameId.ToString());
        await page.WaitForSelectorAsync("[data-testid='join-existing-game']:not([disabled])",
            new() { Timeout = WasmTimeoutMs });
        await page.ClickAsync("[data-testid='join-existing-game']");
        await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
    }

    // MudBlazor's MudDropZone uses HTML5 drag events. Playwright's DragToAsync fires
    // mouse events which don't reliably trigger the HTML5 drag API in headless Chrome,
    // so we dispatch the events directly via JS.
    protected static Task DragDieAsync(IPage page, int index) =>
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

    protected static int[] ParseDiceValues(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("diceValues")
            .EnumerateArray()
            .Select(v => v.GetInt32())
            .ToArray();
    }
}
