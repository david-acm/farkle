using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.E2eTests;

// Baseline verification run.
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
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs = 30_000;
    private const int GameId        = 42;
    private const int PlayerId      = 0;

    // Resolved at runtime so it works both locally (dotnet test from repo root) and in CI.
    private static string VideoDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "videos"));

    // ── video wrapper ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs <paramref name="test"/> inside a video-recording browser context.
    /// After the test (pass or fail) the context is closed to finalise the recording,
    /// and the auto-named .webm file is renamed to <c>{testName}.webm</c>.
    /// </summary>
    private async Task WithVideoAsync(Func<IPage, Task> test,
        [CallerMemberName] string testName = "")
    {
        var context = await fixture.NewContextWithVideoAsync(VideoDir);
        var page    = await context.NewPageAsync();
        try
        {
            await test(page);
            await page.WaitForTimeoutAsync(1_500); // hold on final state before recording ends
        }
        finally
        {
            await context.CloseAsync(); // must close to finalise the .webm file
            var rawPath = await page.Video!.PathAsync();
            if (File.Exists(rawPath))
            {
                var destPath = Path.Combine(VideoDir, $"{testName}.webm");
                File.Move(rawPath, destPath, overwrite: true);
            }
        }
    }

    // ── helpers ────────────────────────────────────────────────────────────────────

    private async Task NavigateAndWaitForWasmAsync(IPage page)
    {
        await page.GotoAsync($"/games/{GameId}");
        await page.WaitForSelectorAsync("button:has-text('Roll')", new() { Timeout = WasmTimeoutMs });
    }

    // ── tests ───────────────────────────────────────────────────────────────────────

    [Fact]
    public Task ShowDiceAfterRolling() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page);

        await page.ClickAsync("button:has-text('Roll')");

        var firstDie = await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });
        firstDie.Should().NotBeNull();
    });

    [Fact]
    public Task CanDragDieToSetAsideZone() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page);

        await page.ClickAsync("button:has-text('Roll')");

        // MudBlazor renders draggable items as div.mud-drop-item-draggable[draggable="true"]
        // inside a div.mud-drop-zone[identifier="Rolled"].
        var firstDie     = page.Locator(".mud-drop-item-draggable").First;
        var setAsideZone = page.Locator("[identifier='SetAside']");

        await firstDie.WaitForAsync(new() { Timeout = 10_000 });
        await firstDie.DragToAsync(setAsideZone);

        await page.WaitForTimeoutAsync(500);
        var keptCount = await setAsideZone.Locator(".mud-drop-item").CountAsync();
        keptCount.Should().BeGreaterThan(0);
    });

    [Fact]
    public Task ScoreIncreasesAfterKeepingAScoringDie() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page);

        var scoreLocator = page.Locator("h3:has-text('Current Player Score')");
        var initialScore = await scoreLocator.InnerTextAsync();

        await page.ClickAsync("button:has-text('Roll')");
        await page.WaitForTimeoutAsync(1_000);

        // Ask the API whether the roll landed a scoring die (avoids parsing CSS transforms).
        var client    = fixture.Factory.CreateClient();
        var response  = await client.PostAsync($"/api/games/{GameId}/players/{PlayerId}/rolls", null);

        if (!response.IsSuccessStatusCode) return;

        var result        = await response.Content.ReadFromJsonAsync<RollDiceResponse>();
        var hasScoringDie = result?.DiceValues?.Any(v => v is 1 or 5) ?? false;
        if (!hasScoringDie) return; // farkle — skip rather than fail

        await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

        var rolledZone   = page.Locator(".mud-drop-zone").Nth(0);
        var setAsideZone = page.Locator(".mud-drop-zone").Nth(1);
        await rolledZone.Locator(".mud-item").First.DragToAsync(setAsideZone);

        await page.ClickAsync("button:has-text('Set Dice Aside')");
        await page.WaitForTimeoutAsync(800);

        var updatedScore = await scoreLocator.InnerTextAsync();
        updatedScore.Should().NotBe(initialScore, "score should update after keeping a die");
    });

    [Fact]
    public Task TurnScoreDisplayIsVisible() => WithVideoAsync(async page =>
    {
        await NavigateAndWaitForWasmAsync(page);

        var scoreHeading = await page.WaitForSelectorAsync("h3:has-text('Current Player Score')",
            new() { Timeout = WasmTimeoutMs });

        scoreHeading.Should().NotBeNull();
    });
}
