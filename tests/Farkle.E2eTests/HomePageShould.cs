namespace Farkle.E2eTests;

[Collection(PlaywrightCollection.Name)]
public class HomePageShould(PlaywrightFixture fixture)
{
    [Fact]
    public async Task ShowHelloWorldAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/");

        var heading = await page.WaitForSelectorAsync("h3", new() { Timeout = 15_000 });
        var text    = await heading!.InnerTextAsync();

        text.Should().Contain("Hello, world!");
    }

    [Fact]
    public async Task ShowNavigationLinksAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/");

        // Wait for the nav menu to render (SSR — no hydration needed)
        await page.WaitForSelectorAsync("nav", new() { Timeout = 15_000 });

        var navHtml = await page.InnerHTMLAsync("nav");
        navHtml.Should().Contain("Home");
        navHtml.Should().Contain("Game");
    }
}
