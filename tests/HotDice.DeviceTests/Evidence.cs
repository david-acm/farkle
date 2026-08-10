using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace HotDice.DeviceTests;

/// <summary>
/// Per-state screenshot capture into the diagnostics dir. Video is captured out-of-process by the
/// wrapper script / CI (<c>xcrun simctl io recordVideo</c> / <c>adb screenrecord</c>), finalized
/// with SIGINT — see <c>docs/mobile-device-testing.md</c>. Frames are numbered so the sequence
/// reads in order, and named by state so a failure is diagnosable from the artifact alone.
/// </summary>
internal sealed class Evidence(AppiumDriver driver, string diagnosticsDir, string platform)
{
    private int _step;

    /// <summary>Captures the current screen as <c>{platform}-{NN}-{label}.png</c>.</summary>
    public void Capture(string label)
    {
        _step++;
        var safe = label.Replace(' ', '-');
        var path = Path.Combine(diagnosticsDir, $"{platform}-{_step:D2}-{safe}.png");
        try
        {
            ((ITakesScreenshot)driver).GetScreenshot().SaveAsFile(path);
        }
        catch (WebDriverException)
        {
            // A screenshot must never mask the real assertion failure — a lost frame is a
            // diagnostic gap, not a test outcome.
        }
    }

    /// <summary>Dumps the current WebView DOM as <c>{platform}-{NN}-{label}.html</c>.</summary>
    public void CapturePageSource(string label)
    {
        _step++;
        var safe = label.Replace(' ', '-');
        var path = Path.Combine(diagnosticsDir, $"{platform}-{_step:D2}-{safe}.html");
        try
        {
            File.WriteAllText(path, driver.PageSource);
        }
        catch (WebDriverException)
        {
            // Same rationale as Capture — best-effort diagnostics, never a masked failure.
        }
    }
}
