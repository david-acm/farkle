using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

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
    private const int WasmTimeoutMs = GameFlow.WasmTimeoutMs;

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

    // Player-advancing helpers (landing nav, start/join, drag die, parse roll) live in
    // the shared GameFlow class so the storyboard capture reuses the exact same flow.

    // ── test ─────────────────────────────────────────────────

    [Fact]
    public Task HappyPath() => WithVideoAsync(async alicePage =>
    {
        // Alice starts a new game from the landing page (she becomes the host).
        // The server generates the game id; capture it to share with Bob.
        var gameId = await GameFlow.StartNewGameFromLandingAsync(alicePage, "Alice");

        // Alice's name was carried via the ?name= query param, so she auto-joins and
        // lands in the lobby — the roster must already show her.
        await alicePage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });
        (await alicePage.Locator("[data-testid='roster-player']").GetByText("Alice").IsVisibleAsync())
            .Should().BeTrue("the host should auto-join and appear in the lobby roster");

        // The host sees the Start button as soon as the lobby loads (a single player
        // is enough to begin; this two-player flow waits for Bob below before starting).
        (await alicePage.WaitForSelectorAsync("[data-testid='start-game-button']", new() { Timeout = 10_000 }))
            .Should().NotBeNull("Alice, as host, should see the Start button in the lobby");

        var bobContext = await fixture.NewContextWithVideoAsync(VideoDir);
        var bobPage    = await bobContext.NewPageAsync();
        try
        {
            // Bob joins the existing game using the share code from the landing page.
            await GameFlow.JoinExistingGameFromLandingAsync(bobPage, "Bob", gameId);
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
            // Each turn keeps one scoring die (~50-100 pts), so reaching 10 000 by
            // legitimate play takes a few hundred turns. The loop breaks the moment
            // someone wins, so this cap only guards against a never-ending game — it
            // does not add runtime. (It was 300 when a now-fixed scoring bug inflated
            // turn scores; see issue #87.)
            const int maxTurns  = 1000;

            for (var turn = 0; turn < maxTurns && !won; turn++)
            {
                var (currentPage, waitingPage) = aliceTurn ? (alicePage, bobPage) : (bobPage, alicePage);

                // Roll — intercept API response to identify scoring dice by index.
                var rollTask = currentPage.WaitForResponseAsync(r => r.Url.Contains("/rolls") && r.Status == 200);
                await currentPage.ClickAsync("button:has-text('Roll')");
                var rollResponse = await rollTask;
                await currentPage.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

                var diceValues = GameFlow.ParseDiceValues(await rollResponse.TextAsync());
                var scoringIdx = Array.FindIndex(diceValues, v => v == 1 || v == 5);

                // Keep the first scoring die (1 = 100 pts, 5 = 50 pts).
                // Farkle (no scoring dice) is valid: just pass with 0 turn score.
                if (scoringIdx >= 0)
                {
                    await GameFlow.DragDieAsync(currentPage, scoringIdx);
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

    // Proves the per-player colour pipeline (#170): the in-turn player's identity colour
    // drives the shared dice/turn UI, every player (in-turn or spectating) sees the same
    // colour, and it flips for everyone when the turn changes — over SignalR, no refresh.
    [Fact]
    public Task ActivePlayerColourDrivesTheSharedUi() => WithVideoAsync(async alicePage =>
    {
        var gameId = await GameFlow.StartNewGameFromLandingAsync(alicePage, "Alice");
        await alicePage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

        var bobContext = await fixture.NewContextWithVideoAsync(VideoDir);
        var bobPage    = await bobContext.NewPageAsync();
        try
        {
            await GameFlow.JoinExistingGameFromLandingAsync(bobPage, "Bob", gameId);
            await bobPage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

            await alicePage.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
                new() { Timeout = 10_000 });
            await alicePage.ClickAsync("[data-testid='start-game-button']");

            // Alice is in turn; Bob waits. Both views must show Alice's colour.
            await alicePage.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 });
            await bobPage.WaitForSelectorAsync("[data-testid='waiting-indicator']", new() { Timeout = 10_000 });

            var aliceColourOnAlice = await ActivePlayerColourAsync(alicePage);
            var aliceColourOnBob   = await ActivePlayerColourAsync(bobPage);
            aliceColourOnAlice.Should().NotBeNullOrWhiteSpace("the in-turn player's colour drives the UI");
            aliceColourOnBob.Should().Be(aliceColourOnAlice,
                "an off-turn spectator sees the in-turn player's colour, not their own");

            // Alice rolls then passes — flips the turn to Bob over SignalR.
            await alicePage.ClickAsync("button:has-text('Roll')");
            await alicePage.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });
            await alicePage.ClickAsync("button:has-text('Pass Turn')");

            await bobPage.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 });

            // The colour flips for everyone, and the two players' colours are distinct.
            var bobColourOnBob   = await ActivePlayerColourAsync(bobPage);
            var bobColourOnAlice = await ActivePlayerColourAsync(alicePage);
            bobColourOnBob.Should().Be(bobColourOnAlice,
                "the active-player colour flips for both players when the turn changes");
            bobColourOnBob.Should().NotBe(aliceColourOnAlice, "each player has a distinct colour");

            await alicePage.WaitForTimeoutAsync(StepDelayMs); // hold the flipped colour on video
        }
        finally
        {
            var bobRawPath = await bobPage.Video!.PathAsync();
            await bobContext.CloseAsync();
            if (File.Exists(bobRawPath))
                File.Move(bobRawPath, Path.Combine(VideoDir, "ActivePlayerColour-Bob.webm"), overwrite: true);
        }
    });

    // Reads the resolved --active-player-color custom property off the in-play container.
    private static async Task<string> ActivePlayerColourAsync(IPage page) =>
        (await page.EvaluateAsync<string>(
            "() => getComputedStyle(document.querySelector('.play-area'))" +
            ".getPropertyValue('--active-player-color').trim()"))!;

    // Proves the refresh/reconnect restore pipeline: a player who reloads mid-turn
    // is put back into the game (no join form), with their turn indicator, scoreboard
    // and rolled dice restored from the server snapshot + the session-stored identity.
    [Fact]
    public Task RestoreStateAfterRefresh() => WithVideoAsync(async alicePage =>
    {
        var gameId = await GameFlow.StartNewGameFromLandingAsync(alicePage, "Alice");
        await alicePage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

        var bobContext = await fixture.NewContextWithVideoAsync(VideoDir);
        var bobPage    = await bobContext.NewPageAsync();
        try
        {
            await GameFlow.JoinExistingGameFromLandingAsync(bobPage, "Bob", gameId);
            await bobPage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

            // Host starts the game; Alice is in turn.
            await alicePage.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
                new() { Timeout = 10_000 });
            await alicePage.ClickAsync("[data-testid='start-game-button']");
            await alicePage.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 });

            // Alice rolls — the game is now mid-turn with her dice on the table.
            await alicePage.ClickAsync("button:has-text('Roll')");
            await alicePage.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });
            var diceBefore = await alicePage.Locator(".die-container").CountAsync();
            diceBefore.Should().BeGreaterThan(0, "Alice rolled, so dice should be on the table");

            // ── Refresh Alice's browser tab ──────────────────────────────
            // sessionStorage survives a reload, so the app should restore her view
            // from GET /api/games/{id} without showing the join form again.
            await alicePage.ReloadAsync(new() { WaitUntil = WaitUntilState.Commit });

            // Back in the game in turn — proves identity + turn were restored.
            (await alicePage.WaitForSelectorAsync("[data-testid='my-turn-indicator']",
                new() { Timeout = WasmTimeoutMs }))
                .Should().NotBeNull("Alice should be restored into her turn after a refresh");

            // The join form must NOT reappear (she is already a player in this game).
            (await alicePage.Locator("button:has-text('Join Game')").CountAsync())
                .Should().Be(0, "a restored player skips the join form");

            // Her rolled dice are restored from the snapshot.
            await alicePage.WaitForSelectorAsync(".die-container", new() { Timeout = WasmTimeoutMs });
            (await alicePage.Locator(".die-container").CountAsync())
                .Should().Be(diceBefore, "the mid-turn dice should be restored after refresh");

            // The scoreboard with both players is restored too.
            (await alicePage.Locator("[data-testid='scoreboard']").InnerTextAsync())
                .Should().Contain("Alice").And.Contain("Bob");

            await alicePage.WaitForTimeoutAsync(StepDelayMs); // hold the restored state on video
        }
        finally
        {
            var bobRawPath = await bobPage.Video!.PathAsync();
            await bobContext.CloseAsync();
            if (File.Exists(bobRawPath))
                File.Move(bobRawPath, Path.Join(VideoDir, "RestoreStateAfterRefresh-Bob.webm"), overwrite: true);
        }
    });
}
