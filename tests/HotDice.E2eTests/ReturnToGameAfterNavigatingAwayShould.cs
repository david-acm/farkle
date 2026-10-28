using Microsoft.Playwright;

namespace HotDice.E2eTests;

/// <summary>
/// #390 — a player who opens another page (Scoring) and comes back must still be live in their game.
/// Both ways back are in-app navigations (no reload): they unmount and remount the game page without
/// restarting WASM — the path a refresh never takes.
/// <list type="bullet">
/// <item>Browser Back: the remount never reopened the realtime connection, so when the host
/// started, the returning player was left on the lobby.</item>
/// <item>The "Play" menu link: it always opened the landing page, so the player lost their game and
/// starting again made a brand-new lobby.</item>
/// </list>
/// </summary>
[Collection(StoryboardCollection.Name)]
[Trait("Category", "Storyboard")]
public class ReturnToGameAfterNavigatingAwayShould(StoryboardFixture fixture)
{
    private const int UiTimeoutMs = 15_000;

    // Bob's frames land beside the storyboard's, so CI's storyboard report publishes them on the PR.
    private static string StoryboardDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "storyboard"));

    [Fact]
    public Task FollowTheHostIntoPlay_AfterVisitingScoringAndGoingBack() =>
        FollowTheHostIntoPlayAfterReturningAsync("back",
            bob => bob.GoBackAsync(new() { WaitUntil = WaitUntilState.Commit }));

    [Fact]
    public Task FollowTheHostIntoPlay_AfterVisitingScoringAndClickingPlay() =>
        FollowTheHostIntoPlayAfterReturningAsync("play",
            bob => bob.GetByRole(AriaRole.Link, new() { Name = "Play" }).ClickAsync());

    private async Task FollowTheHostIntoPlayAfterReturningAsync(string way, Func<IPage, Task> returnToGame)
    {
        var aliceContext = await fixture.NewContextAsync(1280, 800);
        var bobContext   = await fixture.NewContextAsync(1280, 800);
        var alice = await aliceContext.NewPageAsync();
        var bob   = await bobContext.NewPageAsync();
        Directory.CreateDirectory(StoryboardDir);
        Task CaptureAsync(string step) =>
            bob.ScreenshotAsync(new() { Path = Path.Join(StoryboardDir, $"390-{way}-{step}-medium.png"), FullPage = true });
        try
        {
            var gameId = await GameFlow.StartNewGameFromLandingAsync(alice, "Alice");
            await alice.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = UiTimeoutMs });

            await GameFlow.JoinExistingGameFromLandingAsync(bob, "Bob", gameId);
            await bob.WaitForSelectorAsync("[data-testid='waiting-for-host']", new() { Timeout = UiTimeoutMs });
            await CaptureAsync("1-lobby");

            await bob.GetByRole(AriaRole.Link, new() { Name = "Scoring" }).ClickAsync();
            await bob.WaitForURLAsync("**/scoring", new() { Timeout = UiTimeoutMs });
            await CaptureAsync("2-scoring");
            await returnToGame(bob);
            await bob.WaitForURLAsync($"**/games/{gameId}*", new() { Timeout = UiTimeoutMs });
            await bob.WaitForSelectorAsync("[data-testid='waiting-for-host']", new() { Timeout = UiTimeoutMs });
            await CaptureAsync("3-returned");

            await alice.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
                new() { Timeout = UiTimeoutMs });
            await alice.ClickAsync("[data-testid='start-game-button']");

            (await bob.WaitForSelectorAsync("[data-testid='waiting-indicator']", new() { Timeout = UiTimeoutMs }))
                .Should().NotBeNull("Bob should follow the host into play after returning to the game");
        }
        finally
        {
            // Bob's view once the host has started — on a failure, it shows where he got stuck.
            await CaptureAsync("4-after-start");
            await aliceContext.CloseAsync();
            await bobContext.CloseAsync();
        }
    }
}
