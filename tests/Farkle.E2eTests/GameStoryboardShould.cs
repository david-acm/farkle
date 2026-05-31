using System.Text.RegularExpressions;

namespace Farkle.E2eTests;

/// <summary>
/// Captures a storyboard of the <b>host's</b> view — one full-page screenshot taken
/// immediately before every user interaction — through the opening of a game:
/// landing → lobby → roll → drag → keep → pass (the host's first scoring turn).
///
/// It runs at three common screen sizes (mobile, medium desktop, large desktop) so a
/// reviewer can eyeball the responsive layout at each breakpoint. Screenshots land in
/// <c>test-results/screenshots/</c> as <c>{step}-{viewport}.png</c> and are published to
/// the GitHub Pages run report alongside the recordings.
///
/// This is intentionally a separate, short test from <see cref="GameHappyPathShould"/>:
/// it only plays far enough to capture the storyboard, not to a win.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameStoryboardShould(PlaywrightFixture fixture) : GamePlaywrightTest(fixture)
{
    public static IEnumerable<object[]> Viewports =>
    [
        ["mobile",     390,  844],
        ["desktop-md", 1280, 800],
        ["desktop-lg", 1920, 1080],
    ];

    [Theory]
    [MemberData(nameof(Viewports))]
    public async Task CaptureHostStoryboard(string label, int width, int height)
    {
        var viewport     = new ViewportSize { Width = width, Height = height };
        var aliceContext = await Fixture.NewContextWithVideoAsync(VideoDir, viewport);
        var alicePage    = await aliceContext.NewPageAsync();
        IBrowserContext? bobContext = null;
        IPage?           bobPage    = null;
        try
        {
            // 01 — landing page, before the host starts a new game.
            await GotoLandingAndWaitForWasmAsync(alicePage);
            await alicePage.Locator("[placeholder='Your name']").First.FillAsync("Alice");
            await alicePage.WaitForSelectorAsync("[data-testid='start-new-game']:not([disabled])",
                new() { Timeout = WasmTimeoutMs });
            await CaptureAsync(alicePage, "01-landing", label);

            await alicePage.ClickAsync("[data-testid='start-new-game']");
            await alicePage.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
            var gameId = int.Parse(Regex.Match(alicePage.Url, @"/games/(\d+)").Groups[1].Value);
            await alicePage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

            // Bob joins so the host's Start button enables.
            bobContext = await Fixture.NewContextWithVideoAsync(VideoDir, viewport);
            bobPage    = await bobContext.NewPageAsync();
            await JoinExistingGameFromLandingAsync(bobPage, "Bob", gameId);
            await bobPage.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 10_000 });

            // 02 — lobby, before the host starts the game (both players present).
            await alicePage.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
                new() { Timeout = 10_000 });
            await CaptureAsync(alicePage, "02-lobby", label);

            await alicePage.ClickAsync("[data-testid='start-game-button']");
            await alicePage.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 10_000 });

            // Storyboard the host's first scoring turn. A Farkle (no 1s or 5s) can't be
            // kept, so retry whole turns — Bob takes throwaway turns — until Alice rolls a
            // scoring die, keeping the drag/keep frames deterministic.
            var captured  = false;
            var aliceTurn = true;
            for (var turn = 0; turn < 12 && !captured; turn++)
            {
                var currentPage = aliceTurn ? alicePage : bobPage!;
                var waitingPage = aliceTurn ? bobPage! : alicePage;

                // 03 — before roll (start of the host's turn, empty table).
                if (aliceTurn)
                    await CaptureAsync(alicePage, "03-before-roll", label);

                var rollTask = currentPage.WaitForResponseAsync(r => r.Url.Contains("/rolls") && r.Status == 200);
                await currentPage.ClickAsync("button:has-text('Roll')");
                var rollResponse = await rollTask;
                await currentPage.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });
                var scoringIdx = Array.FindIndex(
                    ParseDiceValues(await rollResponse.TextAsync()), v => v == 1 || v == 5);

                if (aliceTurn && scoringIdx >= 0)
                {
                    // 04 — after roll, before dragging a scoring die aside.
                    await CaptureAsync(alicePage, "04-before-drag", label);
                    await DragDieAsync(alicePage, scoringIdx);
                    await alicePage.Locator("[identifier='SetAside']").Locator(".mud-drop-item")
                        .First.WaitForAsync(new() { Timeout = 5_000 });

                    // 05 — after drag, before keeping (Set Dice Aside).
                    await CaptureAsync(alicePage, "05-before-keep", label);
                    await alicePage.ClickAsync("button:has-text('Set Dice Aside')");
                    await alicePage.WaitForTimeoutAsync(200);

                    // 06 — after keep, before passing the turn.
                    await CaptureAsync(alicePage, "06-before-pass", label);
                    await alicePage.ClickAsync("button:has-text('Pass Turn')");
                    captured = true;
                    break;
                }

                // Farkle for Alice, or Bob's throwaway turn: just pass and hand back.
                await currentPage.ClickAsync("button:has-text('Pass Turn')");
                await currentPage.WaitForTimeoutAsync(200);
                await waitingPage.WaitForSelectorAsync("[data-testid='my-turn-indicator']",
                    new() { Timeout = 10_000 });
                aliceTurn = !aliceTurn;
            }

            captured.Should().BeTrue(
                "Alice should roll a scoring die within a few turns to complete the storyboard");

            await alicePage.WaitForTimeoutAsync(1_000); // settle before the recording ends
        }
        finally
        {
            var aliceRaw = await alicePage.Video!.PathAsync();
            await aliceContext.CloseAsync();
            if (File.Exists(aliceRaw))
                File.Move(aliceRaw, Path.Join(VideoDir, $"Storyboard-{label}.webm"), overwrite: true);

            if (bobContext is not null)
            {
                var bobRaw = bobPage is not null ? await bobPage.Video!.PathAsync() : null;
                await bobContext.CloseAsync();
                if (bobRaw is not null && File.Exists(bobRaw))
                    File.Move(bobRaw, Path.Join(VideoDir, $"Storyboard-{label}-Bob.webm"), overwrite: true);
            }
        }
    }

    // Full-page screenshot of the host's view, named {step}-{viewport}.png so the
    // storyboard frames sort in interaction order and group by screen size.
    private static async Task CaptureAsync(IPage page, string step, string label)
    {
        Directory.CreateDirectory(ScreenshotDir);
        await page.ScreenshotAsync(new()
        {
            Path     = Path.Join(ScreenshotDir, $"{step}-{label}.png"),
            FullPage = true,
        });
    }
}
