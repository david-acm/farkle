namespace Farkle.E2eTests;

public class PlaywrightFixture : IAsyncLifetime
{
    public E2eWebAppFactory Factory  { get; private set; } = null!;
    public string           BaseUrl  => Factory.ServerAddress;

    private IPlaywright _playwright = null!;
    private IBrowser    _browser    = null!;

    public async Task InitializeAsync()
    {
        Factory = new E2eWebAppFactory();
        _ = Factory.Server; // triggers CreateHost / Kestrel startup

        _playwright = await Playwright.CreateAsync();
        _browser    = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser.NewContextAsync(new() { BaseURL = BaseUrl });
        return await context.NewPageAsync();
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
