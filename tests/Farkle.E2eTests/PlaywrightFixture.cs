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
