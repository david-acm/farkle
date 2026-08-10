using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Appium.Enums;
using OpenQA.Selenium.Appium.iOS;

namespace HotDice.DeviceTests;

/// <summary>
/// Builds the Appium driver for the target platform and switches it into the app's WebView
/// context, so all element location is ordinary CSS/XPath against the live Blazor DOM (ADR 0011).
/// Also carries the small polling + WebView-context helpers the happy path needs (kept here so the
/// test reads as a flow, not as plumbing). No Selenium.Support dependency — the waits are a plain
/// poll loop.
/// </summary>
internal static class DeviceDriver
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval   = TimeSpan.FromMilliseconds(300);

    // WASM inside a cold WebView can take a while to hydrate on a low-end emulator.
    public static readonly TimeSpan HydrationTimeout = TimeSpan.FromSeconds(120);

    public static AppiumDriver Create(UITestConfig config)
    {
        var options = new AppiumOptions
        {
            PlatformName   = config.IsAndroid ? "Android" : "iOS",
            AutomationName = config.IsAndroid ? "UiAutomator2" : "XCUITest",
            App            = config.AppPath,
        };

        if (config.Udid is { Length: > 0 } udid)
            options.AddAdditionalAppiumOption(MobileCapabilityType.Udid, udid);

        if (config.IsAndroid)
        {
            options.AddAdditionalAppiumOption(AndroidMobileCapabilityType.AutoGrantPermissions, true);
            // The Hybrid app hosts a WebView; make sure Appium can see it and screenshot the web layer.
            options.AddAdditionalAppiumOption("ensureWebviewsHavePages", true);
            options.AddAdditionalAppiumOption("nativeWebScreenshot", true);
            // Automating the WebView needs a Chromedriver matching the System WebView's Chromium
            // version, which isn't bundled. Let Appium fetch the right one on demand (the server must
            // also allow the insecure feature — see the wrapper's `--allow-insecure`).
            options.AddAdditionalAppiumOption("chromedriverAutodownload", true);
            options.AddAdditionalAppiumOption("newCommandTimeout", 300);
            return new AndroidDriver(config.AppiumServerUri, options, DefaultTimeout);
        }

        options.AddAdditionalAppiumOption(IOSMobileCapabilityType.BundleId, config.BundleId);
        options.AddAdditionalAppiumOption("newCommandTimeout", 300);
        return new IOSDriver(config.AppiumServerUri, options, DefaultTimeout);
    }

    /// <summary>
    /// Polls the driver's contexts until a WEBVIEW context appears, then selects it. The cold app
    /// creates its WebView a beat after launch, so this is retried up to the hydration timeout.
    /// </summary>
    public static void SwitchToWebView(this AppiumDriver driver)
    {
        var deadline = DateTime.UtcNow + HydrationTimeout;
        while (DateTime.UtcNow < deadline)
        {
            var webview = driver.Contexts.FirstOrDefault(c => c.Contains("WEBVIEW", StringComparison.OrdinalIgnoreCase));
            if (webview is not null)
            {
                driver.Context = webview;
                return;
            }
            Thread.Sleep(PollInterval);
        }

        throw new WebDriverException(
            $"No WEBVIEW context appeared within {HydrationTimeout.TotalSeconds:N0}s. " +
            $"Contexts seen: {string.Join(", ", driver.Contexts)}. " +
            "A blank/absent WebView is the #1 Hybrid store-rejection risk (docs/mobile-strategy.md).");
    }

    // ── small poll helpers (no Selenium.Support) ─────────────────────────

    public static IWebElement WaitForVisible(this AppiumDriver driver, By by, TimeSpan? timeout = null) =>
        Wait(driver, by, e => e.Displayed, timeout);

    public static IWebElement WaitForEnabled(this AppiumDriver driver, By by, TimeSpan? timeout = null) =>
        Wait(driver, by, e => e.Displayed && e.Enabled, timeout);

    public static int Count(this AppiumDriver driver, By by) => driver.FindElements(by).Count;

    public static bool Exists(this AppiumDriver driver, By by, TimeSpan? timeout = null)
    {
        try { Wait(driver, by, e => e.Displayed, timeout); return true; }
        catch (WebDriverException) { return false; }
    }

    /// <summary>Waits until the browsing context URL matches <paramref name="predicate"/>, returns it.</summary>
    public static string WaitForUrl(this AppiumDriver driver, Func<string, bool> predicate, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? HydrationTimeout);
        while (DateTime.UtcNow < deadline)
        {
            var url = driver.Url;
            if (predicate(url)) return url;
            Thread.Sleep(PollInterval);
        }
        throw new WebDriverException($"URL did not match within timeout. Last URL: {driver.Url}");
    }

    private static IWebElement Wait(AppiumDriver driver, By by, Func<IWebElement, bool> ready, TimeSpan? timeout)
    {
        var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
        WebDriverException? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var el = driver.FindElement(by);
                if (ready(el)) return el;
            }
            catch (WebDriverException ex) { last = ex; }
            Thread.Sleep(PollInterval);
        }
        throw new WebDriverException($"Timed out waiting for {by} to be ready.", last);
    }
}
