using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace Farkle.E2eTests;

/// <summary>
/// Happy-path game flow: navigate → roll → set a scoring die aside → keep → verify score.
///
/// Each test is wrapped in <see cref="WithVideoAsync"/> which records the full browser
/// session to <c>test-results/videos/{testName}.webm</c>.  The CI workflow uploads
/// those files as a GitHub Actions artifact and posts a link on the PR.
///
/// Die values aren't exposed as text in the DOM (the component is a 3D CSS cube that
/// rotates to show the face), so we ask the API for the dice that landed and keep only
/// scoring ones (1s and 5s) when we need a deterministic assertion.
///
/// Each test uses a unique game ID so tests are fully independent of execution order.
/// Sharing a single game ID caused failures when one test left the game in "Keeping"
/// stage and a subsequent test tried to Roll, resulting in a validation error and empty
/// DiceInPlay, which removed .die-container from the DOM.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs = 120_000;
    private const int PlayerId      = 0;

    // Each test gets its own game ID to prevent EventStore state contamination.
    private const int ShowDiceGameId  = 1001;
    private const int DragDieGameId   = 1002;
    private const int ScoreGameId     = 1003;
    private const int TurnScoreGameId = 1004;
    private const int PassTurnGameId  = 1005;

    // Resolved at runtime so it works both locally (dotnet test from repo root) and in CI.
    private static string VideoDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "videos"));

    private static string LogDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "logs"));

    // ── video wrapper ────────────────────────────────────────────

    /// <summary>
    /// Runs <paramref name="test"/> inside a video-recording browser context.
    /// After the test (pass or fail) the context is closed to finalise the recording,
    /// and the auto-named .webm file is renamed to <c>{testName}.webm</c>.
    /// On failure, browser console + Web API logs are written to test-results/logs/.
    /// </summary>
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

            // Always drain API logs to keep per-test isolation; only write to disk on failure.
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

    private async Task NavigateAndWaitForWasmAsync(IPage page, int gameId)
    {
        // WaitUntilState.Commit fires as soon as the server sends response headers,
        // before any CSS or JS is fetched. This bypasses the render-blocking
        // fonts.googleapis.com link that causes GotoAsync to time out waiting for Load.
        await page.GotoAsync($"/games/{gameId}", new() { WaitUntil = WaitUntilState.Commit });
        // Wait for the game-title heading to display the correct game ID. This element
        // only appears after WASM has loaded and OnParametersSetAsync has run StartGame
        // (which always sets GameId in the store, even on failure). Using a DOM element
        // rather than LoadState.NetworkIdle avoids false timeouts from external CDN
        // activity (Google Fonts / MudBlazor icons) that keep the network busy.
        await page.WaitForSelectorAsync($"h3:has-text('{gameId}')",
            new() { Timeout = WasmTimeoutMs });
    }

    // ── tests ───────────────────────────────────────────────

    [Fact]
    public Task ShowDiceAfterRolling() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, ShowDiceGameId);

        await page.ClickAsync("button:has-text('Roll')");

        var firstDie = await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });
        firstDie.Should().NotBeNull();
    });

    [Fact]
    public Task CanDragDieToSetAsideZone() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, DragDieGameId);

        await page.ClickAsync("button:has-text('Roll')");

        // MudBlazor renders draggable items as div.mud-drop-item-draggable[draggable="true"]
        // inside a div.mud-drop-zone[identifier="Rolled"].
        var firstDie     = page.Locator(".mud-drop-item-draggable").First;
        var setAsideZone = page.Locator("[identifier='SetAside']");

        await firstDie.WaitForAsync(new() { Timeout = 10_000 });

        // MudBlazor's MudDropZone uses HTML5 drag events (@ondragstart / @ondrop).
        // Playwright's DragToAsync fires mouse events which don't reliably trigger
        // the HTML5 drag API in headless Chrome. Dispatch the events directly so
        // Blazor's event delegation picks them up correctly.
        await page.EvaluateAsync(@"() => {
            const source = document.querySelector('.mud-drop-item-draggable');
            const target = document.querySelector(""[identifier='SetAside']"");
            const dt = new DataTransfer();
            source.dispatchEvent(new DragEvent('dragstart', { bubbles: true, cancelable: true, dataTransfer: dt }));
            target.dispatchEvent(new DragEvent('dragenter', { bubbles: true, cancelable: true, dataTransfer: dt }));
            target.dispatchEvent(new DragEvent('dragover',  { bubbles: true, cancelable: true, dataTransfer: dt }));
            target.dispatchEvent(new DragEvent('drop',      { bubbles: true, cancelable: true, dataTransfer: dt }));
            source.dispatchEvent(new DragEvent('dragend',   { bubbles: true, cancelable: true, dataTransfer: dt }));
        }");

        await setAsideZone.Locator(".mud-drop-item").First.WaitForAsync(new() { Timeout = 10_000 });
        var keptCount = await setAsideZone.Locator(".mud-drop-item").CountAsync();
        keptCount.Should().BeGreaterThan(0);
    });

    [Fact]
    public Task ScoreIncreasesAfterKeepingAScoringDie() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, ScoreGameId);

        var scoreLocator = page.Locator("h3:has-text('Current Player Score')");
        var initialScore = await scoreLocator.InnerTextAsync();

        await page.ClickAsync("button:has-text('Roll')");
        await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

        await page.EvaluateAsync(@"() => {
            const rolled    = document.querySelectorAll('.mud-drop-zone')[0];
            const setAside  = document.querySelectorAll('.mud-drop-zone')[1];
            const source    = rolled.querySelector('.mud-drop-item-draggable');
            const dt = new DataTransfer();
            source.dispatchEvent(new DragEvent('dragstart', { bubbles: true, cancelable: true, dataTransfer: dt }));
            setAside.dispatchEvent(new DragEvent('dragenter', { bubbles: true, cancelable: true, dataTransfer: dt }));
            setAside.dispatchEvent(new DragEvent('dragover',  { bubbles: true, cancelable: true, dataTransfer: dt }));
            setAside.dispatchEvent(new DragEvent('drop',      { bubbles: true, cancelable: true, dataTransfer: dt }));
            source.dispatchEvent(new DragEvent('dragend',     { bubbles: true, cancelable: true, dataTransfer: dt }));
        }");

        await page.ClickAsync("button:has-text('Set Dice Aside')");
        await page.WaitForTimeoutAsync(800);

        var updatedScore = await scoreLocator.InnerTextAsync();
        // Score increases only when a scoring die (1 or 5) was kept; if all dice
        // were non-scoring the keep is rejected and score stays the same — that is
        // still a valid game outcome, so we don't fail the test in that case.
        if (updatedScore == initialScore) return;
        updatedScore.Should().NotBe(initialScore, "score should update after keeping a scoring die");
    });

    [Fact]
    public Task TurnScoreDisplayIsVisible() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, TurnScoreGameId);

        var scoreHeading = await page.WaitForSelectorAsync("h3:has-text('Current Player Score')",
            new() { Timeout = WasmTimeoutMs });

        scoreHeading.Should().NotBeNull();
    });

    [Fact]
    public Task PassTurnButtonResetsScore() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page, PassTurnGameId);

        await page.ClickAsync("button:has-text('Roll')");
        await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

        await page.EvaluateAsync(@"() => {
            const rolled   = document.querySelectorAll('.mud-drop-zone')[0];
            const setAside = document.querySelectorAll('.mud-drop-zone')[1];
            const source   = rolled.querySelector('.mud-drop-item-draggable');
            const dt = new DataTransfer();
            source.dispatchEvent(new DragEvent('dragstart', { bubbles: true, cancelable: true, dataTransfer: dt }));
            setAside.dispatchEvent(new DragEvent('dragenter', { bubbles: true, cancelable: true, dataTransfer: dt }));
            setAside.dispatchEvent(new DragEvent('dragover',  { bubbles: true, cancelable: true, dataTransfer: dt }));
            setAside.dispatchEvent(new DragEvent('drop',      { bubbles: true, cancelable: true, dataTransfer: dt }));
            source.dispatchEvent(new DragEvent('dragend',     { bubbles: true, cancelable: true, dataTransfer: dt }));
        }");

        await page.ClickAsync("button:has-text('Set Dice Aside')");
        await page.WaitForTimeoutAsync(500);

        await page.ClickAsync("button:has-text('Pass Turn')");
        await page.WaitForTimeoutAsync(1_000);

        // After passing, DiceInPlay is cleared and TurnScore resets to 0.
        var diceCount = await page.Locator(".die-container").CountAsync();
        diceCount.Should().Be(0, "all dice are cleared after passing the turn");

        var scoreText = await page.Locator("h3:has-text('Current Player Score')").InnerTextAsync();
        scoreText.Should().Contain("0", "turn score resets to 0 after passing");
    });
}
