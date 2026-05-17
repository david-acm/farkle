using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Farkle.E2eTests;

/// <summary>
/// Full happy-path game flow in a single browser session:
/// join → roll → drag die → keep → assert score → pass → assert reset.
///
/// A single test keeps CI fast (one WASM hydration instead of many) and mirrors
/// how a real player uses the app. Individual API steps are verified separately
/// by the faster integration tests in Farkle.WebTests.
///
/// Die values aren't exposed in the DOM (the component is a 3D CSS cube), so the
/// roll API response is intercepted to identify scoring dice (1s and 5s) by index.
///
/// The session is recorded to <c>test-results/videos/HappyPath.webm</c> and
/// uploaded as a GitHub Actions artifact on every run.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs    = 120_000;
    private const int GameId           = 1001;

    // Pause between steps so animations are visible in the recorded video.
    // Override with E2E_STEP_DELAY_MS environment variable (e.g. set to 0 for speed).
    private static int StepDelayMs =>
        int.TryParse(Environment.GetEnvironmentVariable("E2E_STEP_DELAY_MS"), out var v) ? v : 2_000;

    private static string VideoDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "videos"));

    private static string LogDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "logs"));

    // ── video wrapper ────────────────────────────────────────────

    private async Task WithVideoAsync(Func<IPage, Task> test,
        [CallerMemberName] string testName = "")
    {
        var context     = await fixture.NewContextWithVideoAsync(VideoDir);
        var page        = await context.NewPageAsync();
        var consoleLogs = new List<string>();

        page.Console   += (_, msg) => consoleLogs.Add($"[{msg.Type.ToUpper()}] {msg.Text}");
        page.PageError += (_, err) => consoleLogs.Add($"[PAGE_ERROR] {err}");

        Exception? failure = null;
        try
        {
            await test(page);
            await page.WaitForTimeoutAsync(1_500); // hold on final state before recording ends
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await context.CloseAsync(); // must close to finalise the .webm file
            var rawPath = await page.Video!.PathAsync();
            if (File.Exists(rawPath))
                File.Move(rawPath, Path.Combine(VideoDir, $"{testName}.webm"), overwrite: true);

            var apiLogs = fixture.Factory.DrainApiLogs();
            if (failure != null)
            {
                Directory.CreateDirectory(LogDir);
                await File.WriteAllLinesAsync(Path.Combine(LogDir, $"{testName}.browser.log"), consoleLogs);
                await File.WriteAllLinesAsync(Path.Combine(LogDir, $"{testName}.api.log"), apiLogs);
            }
        }

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    // ── helpers ──────────────────────────────────────────────

    private async Task NavigateAndWaitForWasmAsync(IPage page, int gameId,
        string playerName = "Tester")
    {
        // WaitUntilState.Commit fires as soon as the server sends response headers,
        // before any CSS or JS is fetched. This bypasses the render-blocking
        // fonts.googleapis.com link that causes GotoAsync to time out waiting for Load.
        await page.GotoAsync($"/games/{gameId}", new() { WaitUntil = WaitUntilState.Commit });
        // Wait for the game-title heading — only present after WASM hydration and StartGame.
        await page.WaitForSelectorAsync($"h3:has-text('{gameId}')",
            new() { Timeout = WasmTimeoutMs });
        await page.FillAsync("[placeholder='Your name']", playerName);
        await page.ClickAsync("button:has-text('Join Game')");
        await page.WaitForSelectorAsync("button:has-text('Roll')", new() { Timeout = 10_000 });
    }

    // MudBlazor's MudDropZone uses HTML5 drag events. Playwright's DragToAsync fires
    // mouse events which don't reliably trigger the HTML5 drag API in headless Chrome,
    // so we dispatch the events directly via JS.
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

    private static int[] ParseDiceValues(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("diceValues")
            .EnumerateArray()
            .Select(v => v.GetInt32())
            .ToArray();
    }

    // ── test ─────────────────────────────────────────────────

    [Fact]
    public Task HappyPath() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, GameId);

        // After joining: turn indicator visible, Roll button enabled
        var indicator = await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']",
            new() { Timeout = 10_000 });
        indicator.Should().NotBeNull("turn indicator should appear after joining");
        (await page.GetAttributeAsync("button:has-text('Roll')", "disabled"))
            .Should().BeNull("Roll button should be enabled when it is the player's turn");

        await page.WaitForTimeoutAsync(StepDelayMs);

        // Intercept the roll API response to identify scoring dice by index —
        // die values are not visible in the DOM (3D CSS cube faces).
        var rollTask = page.WaitForResponseAsync(r => r.Url.Contains("/rolls") && r.Status == 200);
        await page.ClickAsync("button:has-text('Roll')");
        var rollResponse = await rollTask;
        await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });
        await page.WaitForTimeoutAsync(StepDelayMs);

        var diceValues = ParseDiceValues(await rollResponse.TextAsync());
        var scoringIdx = Array.FindIndex(diceValues, v => v == 1 || v == 5);
        var dragIdx    = scoringIdx >= 0 ? scoringIdx : 0; // prefer scoring die; fall back to first

        // Drag the chosen die to SetAside — verifies drag-and-drop interaction
        await DragDieAsync(page, dragIdx);
        var setAsideZone = page.Locator("[identifier='SetAside']");
        await setAsideZone.Locator(".mud-drop-item").First.WaitForAsync(new() { Timeout = 10_000 });
        (await setAsideZone.Locator(".mud-drop-item").CountAsync())
            .Should().BeGreaterThan(0, "die should appear in SetAside after drag");

        await page.WaitForTimeoutAsync(StepDelayMs);

        // Keep and assert score increases when a scoring die was dragged
        var scoreLocator = page.Locator("h3:has-text('Current Player Score')");
        var scoreBefore  = await scoreLocator.InnerTextAsync();
        await page.ClickAsync("button:has-text('Set Dice Aside')");
        await page.WaitForTimeoutAsync(StepDelayMs);

        if (scoringIdx >= 0)
            (await scoreLocator.InnerTextAsync())
                .Should().NotBe(scoreBefore, "score should increase after keeping a scoring die");

        // Pass Turn — dice cleared, turn score resets to 0
        await page.ClickAsync("button:has-text('Pass Turn')");
        await page.WaitForTimeoutAsync(StepDelayMs);

        (await page.Locator(".die-container").CountAsync())
            .Should().Be(0, "all dice should be cleared after passing the turn");
        (await scoreLocator.InnerTextAsync())
            .Should().Contain("0", "turn score should reset to 0 after passing");
    });
}
