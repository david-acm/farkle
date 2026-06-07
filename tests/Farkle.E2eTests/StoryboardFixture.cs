namespace Farkle.E2eTests;

// Slim, viewport-aware Playwright fixture for the storyboard capture. Unlike the
// happy-path fixture it records no video and boots no containers — it just launches
// Chromium against the in-memory host and pre-warms the WASM payload once.
public sealed class StoryboardFixture : IAsyncLifetime
{
    public StoryboardWebAppFactory Factory { get; private set; } = null!;
    public string                  BaseUrl => Factory.ServerAddress;

    private IPlaywright _playwright = null!;
    private IBrowser    _browser    = null!;

    public async Task InitializeAsync()
    {
        Factory = new StoryboardWebAppFactory();
        _ = Factory.Server; // triggers CreateHost / Kestrel startup

        _playwright = await Playwright.CreateAsync();

        var options = new BrowserTypeLaunchOptions
        {
            Headless = true,
            // --no-sandbox: allows launching Chromium as root, which is the norm inside
            //   containers (local dev sandboxes and CI runners).
            // --disable-dev-shm-usage: containers often have a tiny /dev/shm; this routes
            //   shared memory to a temp dir so the browser doesn't crash mid-run.
            Args = ["--no-sandbox", "--disable-dev-shm-usage"],
        };
        // Allow overriding the browser executable when the Playwright-managed download is
        // unavailable (e.g. network-restricted environments) — point at any Chromium build,
        // such as system Chrome/Chromium or Microsoft Edge.
        var execPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH");
        if (!string.IsNullOrEmpty(execPath))
            options.ExecutablePath = execPath;

        _browser = await _playwright.Chromium.LaunchAsync(options);

        await PreWarmWasmAsync();
    }

    // Navigate once so the browser caches the WASM binaries before the first capture,
    // avoiding the full cold-start cost (~60s) inside the timed test.
    private async Task PreWarmWasmAsync()
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = BaseUrl });
        var page    = await context.NewPageAsync();
        try
        {
            await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.Commit });
            await page.WaitForSelectorAsync("[data-testid='start-new-game']", new() { Timeout = 120_000 });
        }
        catch (TimeoutException)
        {
            // Non-fatal: the individual tests will report the real failure.
        }
        catch (PlaywrightException)
        {
            // Non-fatal: the individual tests will report the real failure.
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    // A browser context sized to the given viewport. Each viewport gets its own context.
    public Task<IBrowserContext> NewContextAsync(int width, int height) =>
        _browser.NewContextAsync(new()
        {
            BaseURL      = BaseUrl,
            ViewportSize = new ViewportSize { Width = width, Height = height }
        });

    // HTTP client against the same host — used to add a second player out-of-band so the
    // host can begin the game without driving a second browser.
    public HttpClient CreateApiClient() => new() { BaseAddress = new Uri(BaseUrl) };

    public async Task DisposeAsync()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public class StoryboardCollection : ICollectionFixture<StoryboardFixture>
{
    public const string Name = "Storyboard";
}
