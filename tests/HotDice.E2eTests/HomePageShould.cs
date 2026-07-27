namespace HotDice.E2eTests;

[Collection(PlaywrightCollection.Name)]
public class HomePageShould(PlaywrightFixture fixture)
{
    // The landing page is an interactive WASM page; allow time for hydration.
    private const int WasmTimeoutMs = 120_000;

    [Fact]
    public async Task ShowLandingPageAsync()
    {
        var page = await fixture.NewPageAsync();
        // WaitUntilState.Commit avoids timing out on the render-blocking Google Fonts link.
        await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.Commit });

        // Both entry actions must be present once WASM has hydrated.
        await page.WaitForSelectorAsync("[data-testid='start-new-game']", new() { Timeout = WasmTimeoutMs });
        (await page.QuerySelectorAsync("[data-testid='join-existing-game']"))
            .Should().NotBeNull("the landing page should offer both Start and Join actions");
    }

    [Fact]
    public async Task ShowNavigationLinksAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.Commit });

        // Wait for the nav menu to render (SSR — no hydration needed)
        await page.WaitForSelectorAsync("nav", new() { Timeout = 15_000 });

        var navHtml = await page.InnerHTMLAsync("nav");
        navHtml.Should().Contain("Play");
    }
}
