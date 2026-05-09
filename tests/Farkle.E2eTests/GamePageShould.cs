namespace Farkle.E2eTests;

[Collection(PlaywrightCollection.Name)]
public class GamePageShould(PlaywrightFixture fixture)
{
    // Game page uses InteractiveWebAssembly — allow extra time for WASM hydration.
    private const int WasmTimeoutMs = 30_000;

    [Fact]
    public async Task ShowStartGameButtonAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/games/1");

        var button = await page.WaitForSelectorAsync("button:has-text('Start game')",
            new() { Timeout = WasmTimeoutMs });

        button.Should().NotBeNull();
    }

    [Fact]
    public async Task ShowRollButtonAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/games/1");

        // Wait for WASM hydration
        await page.WaitForSelectorAsync("button:has-text('Start game')",
            new() { Timeout = WasmTimeoutMs });

        var rollButton = await page.QuerySelectorAsync("button:has-text('Roll')");
        rollButton.Should().NotBeNull();
    }

    [Fact]
    public async Task ShowKeepButtonAsync()
    {
        var page = await fixture.NewPageAsync();
        await page.GotoAsync("/games/1");

        await page.WaitForSelectorAsync("button:has-text('Start game')",
            new() { Timeout = WasmTimeoutMs });

        var keepButton = await page.QuerySelectorAsync("button:has-text('Set Dice Aside')");
        keepButton.Should().NotBeNull();
    }
}
