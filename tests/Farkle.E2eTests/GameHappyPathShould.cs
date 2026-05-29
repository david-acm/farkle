using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Farkle.E2eTests;

/// <summary>
/// Full happy-path game flow — two players, one server-generated game:
/// 1. Alice starts a new game from the landing page (server generates the id);
///    Bob joins that game by its share code, both in separate browser contexts.
/// 2. Alice sees the turn indicator; Bob sees the waiting indicator.
/// 3. They alternate turns (roll → optionally keep a scoring die → pass) until one player
///    accumulates 10 000 points and wins.
/// 4. After each pass the other player's indicator flips automatically via SignalR push
///    (no page refresh), proving the real-time turn-change pipeline works end-to-end.
///
/// Both sessions are recorded:
/// <c>HappyPath.webm</c> (Alice) and <c>HappyPath-Bob.webm</c> (Bob).
///
/// Die values aren't exposed in the DOM (3D CSS cube), so the roll API response is
/// intercepted to identify scoring dice (1s and 5s) by index.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs = 120_000;

    // Pause between notable steps so animations are visible in the recorded video.
    // Override with E2E_STEP_DELAY_MS environment variable (e.g. set to 0 for speed).
    private static int StepDelayMs =>
        int.TryParse(Environment.GetEnvironmentVariable("E2E_STEP_DELAY_MS"), out var v) ? v : 2_000;

    private static string VideoDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "videos"));

    private static string LogDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "logs"));

    private static string ScreenshotDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "screenshots"));

    // ── video wrapper ────────────────────────────────────────────

    private async Task WithVideoAsync(Func<IPage, Task> test,
        [CallerMemberName] string testName = "")
    {
        var aliceContext = await fixture.NewContextWithVideoAsync(VideoDir);
        var alicePage    = await aliceContext.NewPageAsync();
        var consoleLogs  = new List<string>();

        alicePage.Console   += (_, msg) => consoleLogs.Add($"[{msg.Type.ToUpper()}] {msg.Text}");
        alicePage.PageError += (_, err) => consoleLogs.Add($"[PAGE_ERROR] {err}");

        Exception? failure = null;
        try
        {
            await test(alicePage);
            await alicePage.WaitForTimeoutAsync(1_500); // hold on final state before recording ends
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await aliceContext.CloseAsync(); // must close to finalise the .webm file
            var rawPath = await alicePage.Video!.PathAsync();
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

    // Loads the landing page and waits for WASM hydration (the Start button appears).
    // Full-page navigation (GotoAsync) restarts WASM, giving a fresh BlazorState store
    // with no player-specific data from a previous game session.
    private async Task GotoLandingAndWaitForWasmAsync(IPage page)
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
    private async Task<int> StartNewGameFromLandingAsync(IPage page, string playerName)
    {
        await GotoLandingAndWaitForWasmAsync(page);
        // The Start card's name field is the first 'Your name' input.
        await page.Locator("[placeholder='Your name']").First.FillAsync(playerName);
        await page.ClickAsync("[data-testid='start-new-game']");
        await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
        var match = Regex.Match(page.Url, @"/games/(\d+)");
        return int.Parse(match.Groups[1].Value);
    }

    // Joins an existing game from the landing page using its share code.
    private async Task JoinExistingGameFromLandingAsync(IPage page, string playerName, int gameId)
    {
        await GotoLandingAndWaitForWasmAsync(page);
        // The Join card's name field is the second 'Your name' input.
        await page.Locator("[placeholder='Your name']").Last.FillAsync(playerName);
        await page.FillAsync("[placeholder='Game code']", gameId.ToString());
        await page.ClickAsync("[data-testid='join-existing-game']");
        await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
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
    public Task HappyPath() => WithVideoAsync(async alicePage =>
    {
        // Alice starts a new game from the landing page (she becomes the host).
        // The server generates the game id; capture it to share with Bob.
        var gameId = await StartNewGameFromLandingAsync(alicePage, "Alice");

        // Alice's name was carried via the ?name= query param, so she auto-joins and
        // lands in the lobby — the roster must already show her.
        await alicePage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });
        (await alicePage.Locator("[data-testid='roster-player']").GetByText("Alice").IsVisibleAsync())
            .Should().BeTrue("the host should auto-join and appear in the lobby roster");

        // The host's Start button is disabled until a second player joins.
        (await alicePage.WaitForSelectorAsync("[data-testid='start-game-button']", new() { Timeout = 10_000 }))
            .Should().NotBeNull("Alice, as host, should see the Start button in the lobby");

        var bobContext = await fixture.NewContextWithVideoAsync(VideoDir);
        var bobPage    = await bobContext.NewPageAsync();
        try
        {
            // Bob joins the existing game using the share code from the landing page.
            await JoinExistingGameFromLandingAsync(bobPage, "Bob", gameId);
            await bobPage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

            (await bobPage.WaitForSelectorAsync("[data-testid='waiting-for-host']", new() { Timeout = 10_000 }))
                .Should().NotBeNull("Bob, as a non-host, should be waiting for the host to start");

            // Once Bob's join reaches Alice over SignalR, the host's Start button enables.
            await alicePage.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
                new() { Timeout = 10_000 });
            await alicePage.ClickAsync("[data-testid='start-game-button']");

            // Both players drop into play: Alice is in turn, Bob waits — the GameBegan
            // broadcast flips Bob's view without a refresh.
            (await alicePage.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 }))
                .Should().NotBeNull("Alice should be in turn once the game begins");
            (await bobPage.WaitForSelectorAsync("[data-testid='waiting-indicator']", new() { Timeout = 10_000 }))
                .Should().NotBeNull("Bob should see the waiting indicator while Alice is in turn");

            // ── Two-player winning loop ──────────────────────────────────
            // Alice and Bob alternate turns. After each pass the SignalR hub broadcasts
            // TurnChanged so the other player's indicator flips without a page refresh.
            // The loop continues until one player reaches 10 000 points.
            var won             = false;
            var aliceTurn       = true;
            var screenshotTaken = false;
            const int maxTurns  = 300;

            for (var turn = 0; turn < maxTurns && !won; turn++)
            {
                var (currentPage, waitingPage) = aliceTurn ? (alicePage, bobPage) : (bobPage, alicePage);

                // Roll — intercept API response to identify scoring dice by index.
                var rollTask = currentPage.WaitForResponseAsync(r => r.Url.Contains("/rolls") && r.Status == 200);
                await currentPage.ClickAsync("button:has-text('Roll')");
                var rollResponse = await rollTask;
                await currentPage.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

                var diceValues = ParseDiceValues(await rollResponse.TextAsync());
                var scoringIdx = Array.FindIndex(diceValues, v => v == 1 || v == 5);

                // Keep the first scoring die (1 = 100 pts, 5 = 50 pts).
                // Farkle (no scoring dice) is valid: just pass with 0 turn score.
                if (scoringIdx >= 0)
                {
                    await DragDieAsync(currentPage, scoringIdx);
                    await currentPage.Locator("[identifier='SetAside']").Locator(".mud-drop-item")
                        .First.WaitForAsync(new() { Timeout = 5_000 });

                    if (aliceTurn && !screenshotTaken)
                    {
                        Directory.CreateDirectory(ScreenshotDir);
                        await currentPage.ScreenshotAsync(new()
                        {
                            Path     = Path.Join(ScreenshotDir, "before-first-keep.png"),
                            FullPage = true,
                        });
                        screenshotTaken = true;
                    }

                    await currentPage.ClickAsync("button:has-text('Set Dice Aside')");
                    await currentPage.WaitForTimeoutAsync(200);
                }

                await currentPage.ClickAsync("button:has-text('Pass Turn')");
                await currentPage.WaitForTimeoutAsync(200);

                if (await currentPage.Locator("[data-testid='scoreboard']").GetByText("wins!").IsVisibleAsync() ||
                    await waitingPage.Locator("[data-testid='scoreboard']").GetByText("wins!").IsVisibleAsync())
                {
                    won = true;
                    break;
                }

                // Waiting player's browser must flip to my-turn via SignalR — no page refresh.
                await waitingPage.WaitForSelectorAsync("[data-testid='my-turn-indicator']",
                    new() { Timeout = 10_000 });

                aliceTurn = !aliceTurn;
            }

            await alicePage.WaitForTimeoutAsync(StepDelayMs); // pause on the winner screen

            won.Should().BeTrue($"a player should win within {maxTurns} turns");
            var scoreboards = (await alicePage.Locator("[data-testid='scoreboard']").InnerTextAsync()) +
                              (await bobPage.Locator("[data-testid='scoreboard']").InnerTextAsync());
            scoreboards.Should().Contain("wins!", "winner should be announced in the scoreboard");
        }
        finally
        {
            var bobRawPath = await bobPage.Video!.PathAsync();
            await bobContext.CloseAsync();
            if (File.Exists(bobRawPath))
                File.Move(bobRawPath, Path.Combine(VideoDir, "HappyPath-Bob.webm"), overwrite: true);
        }
    });
}
