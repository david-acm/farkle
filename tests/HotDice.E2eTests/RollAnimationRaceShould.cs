using Microsoft.Playwright;

namespace HotDice.E2eTests;

/// <summary>
/// Regression guard for the intermittent "dice appear but don't roll" bug: the roll spin is a
/// two-phase mount (paint the die's neutral orientation, then rotate to the face so the CSS
/// transition animates). If the rotation lands before the neutral frame paints, the dice snap
/// to their faces with no spin. It's a race — worst on the first roll of a turn (empty table →
/// Roll) — so this rolls repeatedly in a single-player game and asserts EVERY roll animates.
///
/// Detection: right after the dice appear, sample the first die's computed transform every ~15ms
/// for ~300ms (well under the 1.4s transition). A spinning die interpolates through many distinct
/// matrices; a snapped die reports a single, unchanging transform.
///
/// Infra-light (Storyboard collection → in-memory store, no Docker); runs in the storyboard CI job.
/// </summary>
[Collection(StoryboardCollection.Name)]
[Trait("Category", "Storyboard")]
public class RollAnimationRaceShould(StoryboardFixture fixture)
{
    private const int Rolls = 15;

    [Fact]
    public async Task SpinOnEveryRoll()
    {
        var context = await fixture.NewContextAsync(1280, 800);
        var page    = await context.NewPageAsync();
        try
        {
            await GameFlow.GotoLandingAndWaitForWasmAsync(page);
            await GameFlow.StartNewGameAsync(page, "Alice");
            await page.WaitForSelectorAsync("[data-testid='lobby']", new() { Timeout = 15_000 });

            // A single player can begin (HasMinimumPlayers.Minimum == 1). Reload so the host's
            // Start button re-reads the roster from the server snapshot and enables.
            await page.ReloadAsync(new() { WaitUntil = WaitUntilState.Commit });
            await page.WaitForSelectorAsync("[data-testid='start-game-button']:not([disabled])",
                new() { Timeout = GameFlow.WasmTimeoutMs });
            await page.ClickAsync("[data-testid='start-game-button']");
            await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 15_000 });

            var noSpin = 0;
            for (var i = 0; i < Rolls; i++)
            {
                await page.ClickAsync("button:has-text('Roll')");
                await page.WaitForSelectorAsync(".die-container .die", new() { Timeout = 15_000 });

                var distinctTransforms = await page.EvaluateAsync<int>(@"() => new Promise(resolve => {
                    const el = document.querySelector('.die-container .die');
                    if (!el) { resolve(0); return; }
                    const seen = new Set();
                    let n = 0;
                    const id = setInterval(() => {
                        seen.add(getComputedStyle(el).transform);
                        if (++n >= 20) { clearInterval(id); resolve(seen.size); }
                    }, 15);
                })");

                if (distinctTransforms <= 1) noSpin++;

                await page.ClickAsync("button:has-text('Pass Turn')");
                await page.WaitForSelectorAsync("[data-testid='my-turn-indicator']", new() { Timeout = 15_000 });
                await page.WaitForTimeoutAsync(150);
            }

            noSpin.Should().Be(0,
                $"every roll must animate, but {noSpin}/{Rolls} snapped to their faces with no spin");
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
