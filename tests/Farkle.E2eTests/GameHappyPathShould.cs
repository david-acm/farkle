using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Farkle.E2eTests;

/// <summary>
/// Two E2E tests covering the happy path:
///
/// 1. <see cref="HappyPath"/> — single-player flow in one browser session:
///    join → roll → drag → keep → assert score → pass → assert reset.
///    Recorded to <c>test-results/videos/HappyPath.webm</c>.
///
/// 2. <see cref="MultiplayerTwoPlayersCanPlay"/> — two independent browser
///    contexts join the same game; verifies P1 sees their turn indicator and
///    P2 immediately sees the waiting indicator via <c>CurrentPlayerId</c>
///    from the join response. Recorded to <c>MultiplayerTwoPlayersCanPlay.webm</c>
///    and <c>MultiplayerTwoPlayersCanPlay-Bob.webm</c>.
///
/// Die values aren't exposed in the DOM (the component is a 3D CSS cube), so the
/// roll API response is intercepted to identify scoring dice (1s and 5s) by index.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs    = 120_000;
    private const int GameId           = 1001;
    private const int MultiplayerGameId = 1008;

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

    // ── tests ─────────────────────────────────────────────────

    [Fact]
    public Task HappyPath() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, GameId);

        // After joining: turn indicator visible, Roll button enabled.
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

        // Drag the chosen die to SetAside — verifies drag-and-drop interaction.
        await DragDieAsync(page, dragIdx);
        var setAsideZone = page.Locator("[identifier='SetAside']");
        await setAsideZone.Locator(".mud-drop-item").First.WaitForAsync(new() { Timeout = 10_000 });
        (await setAsideZone.Locator(".mud-drop-item").CountAsync())
            .Should().BeGreaterThan(0, "die should appear in SetAside after drag");

        await page.WaitForTimeoutAsync(StepDelayMs);

        // Keep and assert score increases when a scoring die was dragged.
        var scoreLocator = page.Locator("h3:has-text('Current Player Score')");
        var scoreBefore  = await scoreLocator.InnerTextAsync();
        await page.ClickAsync("button:has-text('Set Dice Aside')");
        await page.WaitForTimeoutAsync(StepDelayMs);

        if (scoringIdx >= 0)
            (await scoreLocator.InnerTextAsync())
                .Should().NotBe(scoreBefore, "score should increase after keeping a scoring die");

        // Pass Turn — dice cleared, turn score resets to 0.
        await page.ClickAsync("button:has-text('Pass Turn')");
        await page.WaitForTimeoutAsync(StepDelayMs);

        (await page.Locator(".die-container").CountAsync())
            .Should().Be(0, "all dice should be cleared after passing the turn");
        (await scoreLocator.InnerTextAsync())
            .Should().Contain("0", "turn score should reset to 0 after passing");
    });

    [Fact]
    public Task MultiplayerTwoPlayersCanPlay() => WithVideoAsync(async page =>
    {
        // Player 1 joins in the main (video-recorded) context.
        await NavigateAndWaitForWasmAsync(page, MultiplayerGameId, "Alice");

        (await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 }))
            .Should().NotBeNull("Player 1 should see their turn indicator after joining");

        // Player 2 joins in an independent browser context (separate session/cookies).
        // The WASM client has no real-time push, so P2's state is set once at join time
        // via the CurrentPlayerId field in JoinPlayerResponse.
        var context2 = await fixture.NewContextWithVideoAsync(VideoDir);
        var page2    = await context2.NewPageAsync();
        try
        {
            await NavigateAndWaitForWasmAsync(page2, MultiplayerGameId, "Bob");

            // Player 2 correctly sees the waiting indicator — the join response carries
            // CurrentPlayerId so the client knows immediately it is not their turn.
            (await page2.WaitForSelectorAsync("[data-testid='waiting-indicator']", new() { Timeout = 10_000 }))
                .Should().NotBeNull("Player 2 should be waiting while Player 1 is in turn");
        }
        finally
        {
            var rawPath2 = await page2.Video!.PathAsync();
            await context2.CloseAsync();
            if (File.Exists(rawPath2))
                File.Move(rawPath2, Path.Combine(VideoDir, "MultiplayerTwoPlayersCanPlay-Bob.webm"), overwrite: true);
        }
    });
}
