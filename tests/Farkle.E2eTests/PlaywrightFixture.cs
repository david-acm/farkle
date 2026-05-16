namespace Farkle.E2eTests;

public class PlaywrightFixture : IAsyncLifetime
{
    public E2EWebAppFactory Factory  { get; private set; } = null!;
    public string           BaseUrl  => Factory.ServerAddress;

    private IPlaywright _playwright = null!;
    private IBrowser    _browser    = null!;

    public async Task InitializeAsync()
    {
        Factory = new E2EWebAppFactory();
        _ = Factory.Server; // triggers CreateHost / Kestrel startup

        _playwright = await Playwright.CreateAsync();

        var options = new BrowserTypeLaunchOptions { Headless = true };
        // Allow overriding the browser executable for environments where the
        // Playwright-managed download is unavailable (e.g. network-restricted CI).
        var execPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH");
        if (!string.IsNullOrEmpty(execPath))
            options.ExecutablePath = execPath;

        _browser = await _playwright.Chromium.LaunchAsync(options);

        // Pre-warm: navigate to a game page once so the browser caches all WASM
        // binaries before any test runs. Without this, the first test in a filtered
        // CI run bears the full cold-start cost (~60+ s on a 2-vCPU runner) and
        // times out. Game 999 is used to avoid conflicting with test game IDs.
        await PreWarmWasmAsync();
    }

    private async Task PreWarmWasmAsync()
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = BaseUrl });
        var page    = await context.NewPageAsync();
        try
        {
            // WaitUntilState.Commit fires on the first server byte — never blocked by
            // render-blocking CSS (fonts.googleapis.com) unlike the default Load event.
            await page.GotoAsync("/games/999", new() { WaitUntil = WaitUntilState.Commit });
            // Wait for the game-title heading — confirms WASM downloaded, the .NET runtime
            // started, and StartGame completed. Avoids depending on LoadState.NetworkIdle
            // which never settles when the page has external CDN links (Google Fonts).
            await page.WaitForSelectorAsync("h3:has-text('999')", new() { Timeout = 120_000 });
            await page.FillAsync("[placeholder='Your name']", "Warm-up");
            await page.ClickAsync("button:has-text('Join Game')");
            await page.WaitForSelectorAsync("button:has-text('Roll')", new() { Timeout = 10_000 });
            await page.ClickAsync("button:has-text('Roll')", new() { Timeout = 30_000 });
            // Give the Roll API call time to complete so EventStore's write path is warm.
            await page.WaitForTimeoutAsync(3_000);
        }
        catch
        {
            // Non-fatal: if warmup times out the individual tests will report the real failure.
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = BaseUrl });
        return await context.NewPageAsync();
    }

    /// <summary>
    /// Creates a browser context that records video into <paramref name="videoDir"/>.
    /// Caller must close the context after the test to finalise the recording, then
    /// call <c>page.Video!.PathAsync()</c> to retrieve the generated file path.
    /// </summary>
    public async Task<IBrowserContext> NewContextWithVideoAsync(string videoDir)
    {
        Directory.CreateDirectory(videoDir);
        return await _browser.NewContextAsync(new()
        {
            BaseURL       = BaseUrl,
            RecordVideoDir  = videoDir,
            RecordVideoSize = new RecordVideoSize { Width = 1280, Height = 720 }
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
