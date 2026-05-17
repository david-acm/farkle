using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Farkle.E2eTests;

/// <summary>
/// Full happy-path game flow:
/// 1. Multiplayer indicator check — Alice (P1) and Bob (P2) join game 1008 in separate
///    browser contexts; Alice sees the turn indicator and Bob sees the waiting indicator.
/// 2. Winning loop — Alice navigates to a fresh solo game (1001) and plays turn after
///    turn (roll → keep all scoring dice → pass) until she reaches 10 000 and wins.
///
/// Full-page navigation between the two games restarts WASM with fresh state so the
/// join form appears correctly for each game. Both sessions are recorded:
/// <c>HappyPath.webm</c> (Alice) and <c>HappyPath-Bob.webm</c> (Bob's brief session).
///
/// Die values aren't exposed in the DOM (3D CSS cube), so the roll API response is
/// intercepted to identify scoring dice (1s and 5s) by index.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs     = 120_000;
    private const int GameId            = 1001; // solo winning game — Alice only
    private const int MultiplayerGameId = 1008; // multiplayer indicator check — Alice + Bob

    // Pause between notable steps so animations are visible in the recorded video.
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

    // Full-page navigation (GotoAsync) restarts WASM, giving a fresh BlazorState store
    // with no player-specific data from a previous game session.
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
        // ── Multiplayer indicator check ──────────────────────────────
        // Alice joins first; Bob joins in a second context. Verifies that
        // CurrentPlayerId in the join response drives the correct indicator
        // for each player without any real-time push from the server.
        await NavigateAndWaitForWasmAsync(page, MultiplayerGameId, "Alice");

        (await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 }))
            .Should().NotBeNull("Alice should see the turn indicator after joining");

        var context2 = await fixture.NewContextWithVideoAsync(VideoDir);
        var page2    = await context2.NewPageAsync();
        try
        {
            await NavigateAndWaitForWasmAsync(page2, MultiplayerGameId, "Bob");

            (await page2.WaitForSelectorAsync("[data-testid='waiting-indicator']", new() { Timeout = 10_000 }))
                .Should().NotBeNull("Bob should see the waiting indicator while Alice is in turn");

            // Alice rolls and passes — SignalR hub will broadcast TurnChanged to Bob's session.
            var rollTask = page.WaitForResponseAsync(r => r.Url.Contains("/rolls") && r.Status == 200);
            await page.ClickAsync("button:has-text('Roll')");
            await rollTask;
            await page.ClickAsync("button:has-text('Pass Turn')");
            await page.WaitForTimeoutAsync(500); // allow hub push to propagate

            // Bob's indicator should flip automatically via SignalR — no page refresh required.
            (await page2.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 }))
                .Should().NotBeNull("Bob should see my-turn-indicator after Alice passes via SignalR push");

            await page2.WaitForTimeoutAsync(StepDelayMs);
        }
        finally
        {
            var rawPath2 = await page2.Video!.PathAsync();
            await context2.CloseAsync();
            if (File.Exists(rawPath2))
                File.Move(rawPath2, Path.Combine(VideoDir, "HappyPath-Bob.webm"), overwrite: true);
        }

        // ── Play to win ──────────────────────────────────────────────
        // Full-page navigation to a solo game restarts WASM so Alice can join fresh.
        // With only one player the turn always rotates back to Alice, allowing an
        // uninterrupted loop until she accumulates 10 000 points.
        await NavigateAndWaitForWasmAsync(page, GameId, "Alice");

        (await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 }))
            .Should().NotBeNull("Alice should be in turn at the start of the solo game");

        var won      = false;
        const int maxTurns = 300;

        for (var turn = 0; turn < maxTurns; turn++)
        {
            // Roll — intercept the API response to find scoring dice by index.
            var rollTask     = page.WaitForResponseAsync(r => r.Url.Contains("/rolls") && r.Status == 200);
            await page.ClickAsync("button:has-text('Roll')");
            var rollResponse = await rollTask;
            await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

            var diceValues = ParseDiceValues(await rollResponse.TextAsync());
            var scoringIdx = Array.FindIndex(diceValues, v => v == 1 || v == 5);

            // Keep the first scoring die (1 = 100 pts, 5 = 50 pts).
            // Farkle (no scoring dice) is valid: just pass with 0 turn score.
            if (scoringIdx >= 0)
            {
                await DragDieAsync(page, scoringIdx);
                await page.Locator("[identifier='SetAside']").Locator(".mud-drop-item")
                    .First.WaitForAsync(new() { Timeout = 5_000 });
                await page.ClickAsync("button:has-text('Set Dice Aside')");
                await page.WaitForTimeoutAsync(200);
            }

            await page.ClickAsync("button:has-text('Pass Turn')");
            await page.WaitForTimeoutAsync(200);

            // The PassTurn response sets WinnerName in the store; the scoreboard then
            // shows "🏆 Alice wins!" inside the [data-testid='scoreboard'] element.
            if (await page.Locator("[data-testid='scoreboard']").GetByText("wins!").IsVisibleAsync())
            {
                won = true;
                break;
            }
        }

        await page.WaitForTimeoutAsync(StepDelayMs); // pause on the winner screen

        won.Should().BeTrue($"Alice should win within {maxTurns} turns");
        (await page.Locator("[data-testid='scoreboard']").InnerTextAsync())
            .Should().Contain("wins!", "winner should be announced in the scoreboard");
    });
}
