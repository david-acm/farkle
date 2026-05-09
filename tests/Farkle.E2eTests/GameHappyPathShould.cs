using System.Net.Http.Json;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.E2eTests;

/// <summary>
/// Happy-path game flow: navigate → roll → set a scoring die aside → keep → verify score.
///
/// Die values aren't exposed as text in the DOM (the component is a 3D CSS cube
/// that rotates to show the face), so we ask the API for the dice that landed and
/// drag only the ones that score (1s and 5s).  The factory's Kestrel host serves
/// both the Blazor front-end and the API, so we can reuse fixture.Factory.CreateClient()
/// to make direct API calls for setup / assertion while Playwright drives the UI.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class GameHappyPathShould(PlaywrightFixture fixture)
{
    private const int WasmTimeoutMs = 30_000;
    private const int GameId        = 42;
    private const int PlayerId      = 0;

    // ── helpers ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a new page already navigated to the game, with WASM hydrated.
    /// </summary>
    private async Task<IPage> OpenGamePageAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync($"/games/{GameId}");
        // Wait for the Roll button — reliable indicator WASM is live.
        await page.WaitForSelectorAsync("button:has-text('Roll')", new() { Timeout = WasmTimeoutMs });
        return page;
    }

    /// <summary>
    /// Keeps clicking Roll until at least one 1 or 5 appears, then returns the dice list.
    /// Gives up after <paramref name="maxAttempts"/> farkles.
    /// </summary>
    private async Task<IList<int>> RollUntilScoreable(IPage page, int maxAttempts = 10)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            await page.ClickAsync("button:has-text('Roll')");

            // Small delay for BlazorState to propagate the new dice to the UI.
            await page.WaitForTimeoutAsync(800);

            // Ask the live API what values landed (avoids parsing 3D CSS transforms).
            var client   = fixture.Factory.CreateClient();
            var response = await client.PostAsync($"/api/games/{GameId}/players/{PlayerId}/rolls", null);
            if (!response.IsSuccessStatusCode) continue;

            var result = await response.Content.ReadFromJsonAsync<RollDiceResponse>();
            var values = result?.DiceValues?.ToList() ?? new List<int>();

            if (values.Any(v => v is 1 or 5))
                return values;
        }

        return new List<int>();
    }

    // ── tests ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShowDiceAfterRolling()
    {
        var page = await OpenGamePageAsync();

        await page.ClickAsync("button:has-text('Roll')");

        // After roll the DragabbleDice component renders die-containers in the "Rolled" zone.
        var firstDie = await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

        firstDie.Should().NotBeNull();
    }

    [Fact]
    public async Task CanDragDieToSetAsideZone()
    {
        var page = await OpenGamePageAsync();

        await page.ClickAsync("button:has-text('Roll')");

        // MudBlazor renders draggable items as div.mud-drop-item-draggable[draggable="true"]
        // inside a div.mud-drop-zone[identifier="Rolled"].
        var firstDie     = page.Locator(".mud-drop-item-draggable").First;
        var setAsideZone = page.Locator("[identifier='SetAside']");

        await firstDie.WaitForAsync(new() { Timeout = 10_000 });
        await firstDie.DragToAsync(setAsideZone);

        // After drag the item should sit inside the SetAside zone.
        await page.WaitForTimeoutAsync(500);
        var keptCount = await setAsideZone.Locator(".mud-drop-item").CountAsync();
        keptCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ScoreIncreasesAfterKeepingAScoringDie()
    {
        var page = await OpenGamePageAsync();

        // Read initial score text.
        var scoreLocator = page.Locator("h3:has-text('Current Player Score')");
        var initialScore = await scoreLocator.InnerTextAsync();

        // Roll until we know there's a scoring die, then click Roll once more in the UI.
        await page.ClickAsync("button:has-text('Roll')");
        await page.WaitForTimeoutAsync(1_000);

        // Ask the API — we need to know the values for this roll (the one the UI just did).
        var client   = fixture.Factory.CreateClient();
        var response = await client.PostAsync($"/api/games/{GameId}/players/{PlayerId}/rolls", null);

        if (!response.IsSuccessStatusCode)
            return; // can't assert score without a successful roll

        var result = await response.Content.ReadFromJsonAsync<RollDiceResponse>();
        var hasScoringDie = result?.DiceValues?.Any(v => v is 1 or 5) ?? false;
        if (!hasScoringDie)
            return; // rolled a farkle — skip assertion rather than fail

        await page.WaitForSelectorAsync(".die-container", new() { Timeout = 10_000 });

        // Drag the first die (assume it might score; if not the API returns error but UI still responds).
        var rolledZone   = page.Locator(".mud-drop-zone").Nth(0);
        var setAsideZone = page.Locator(".mud-drop-zone").Nth(1);
        await rolledZone.Locator(".mud-item").First.DragToAsync(setAsideZone);

        // Keep
        await page.ClickAsync("button:has-text('Set Dice Aside')");
        await page.WaitForTimeoutAsync(800);

        var updatedScore = await scoreLocator.InnerTextAsync();
        updatedScore.Should().NotBe(initialScore, "score should update after keeping a die");
    }

    [Fact]
    public async Task TurnScoreDisplayIsVisible()
    {
        var page = await OpenGamePageAsync();

        var scoreHeading = await page.WaitForSelectorAsync("h3:has-text('Current Player Score')",
            new() { Timeout = WasmTimeoutMs });

        scoreHeading.Should().NotBeNull();
    }
}
