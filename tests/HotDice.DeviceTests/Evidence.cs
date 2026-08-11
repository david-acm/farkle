using System.Diagnostics;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;

namespace HotDice.DeviceTests;

/// <summary>
/// Per-state evidence into the diagnostics dir. Screenshots are captured at the DEVICE level
/// (<c>adb exec-out screencap</c> / <c>xcrun simctl io screenshot</c>), not through Appium: the
/// driver's <c>GetScreenshot</c> silently no-ops in the Android WebView context, so a device-level
/// grab of the framebuffer is the only thing that reliably yields a real frame. Video is captured
/// out-of-process by the wrapper (<c>adb screenrecord</c> / <c>simctl recordVideo</c>, SIGINT-finalized).
/// Frames are numbered so the sequence reads in order and named by state so a failure is diagnosable.
/// </summary>
internal sealed class Evidence(AppiumDriver driver, UITestConfig config)
{
    private readonly string _platform = config.IsAndroid ? "android" : "ios";
    private int _step;

    /// <summary>Captures the current device screen as <c>{platform}-{NN}-{label}.png</c>.</summary>
    public void Capture(string label)
    {
        var path = NextPath(label, "png");
        try
        {
            if (config.IsAndroid)
            {
                // `exec-out screencap -p` streams raw PNG bytes on stdout — captures the whole screen
                // (WebView included), unlike the driver's context-scoped screenshot.
                var png = RunForStdout("adb", WithUdid("exec-out", "screencap", "-p"));
                if (png is { Length: > 0 }) File.WriteAllBytes(path, png);
            }
            else
            {
                Run("xcrun", "simctl", "io", config.Udid ?? "booted", "screenshot", path);
            }
        }
        catch
        {
            // Best-effort: a lost frame is a diagnostic gap, never a masked test failure. The
            // wrapper's evidence guard fails the run if NO frames land, so a silent gap can't hide.
        }
    }

    /// <summary>Dumps the current WebView DOM as <c>{platform}-{NN}-{label}.html</c>.</summary>
    public void CapturePageSource(string label)
    {
        var path = NextPath(label, "html");
        try { File.WriteAllText(path, driver.PageSource); }
        catch (WebDriverException) { }
    }

    private string NextPath(string label, string ext)
    {
        _step++;
        var safe = label.Replace(' ', '-');
        return Path.Combine(config.DiagnosticsDir, $"{_platform}-{_step:D2}-{safe}.{ext}");
    }

    // Pin adb to the target device when a UDID is known (an unpinned adb picks the wrong one when
    // several are attached — the same gotcha the runbook calls out).
    private string[] WithUdid(params string[] tail) =>
        config.Udid is { Length: > 0 } u ? new[] { "-s", u }.Concat(tail).ToArray() : tail;

    private static byte[]? RunForStdout(string file, params string[] args)
    {
        using var process = Start(file, args, redirectStdout: true);
        if (process is null) return null;
        using var buffer = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(buffer);
        process.WaitForExit(15_000);
        return buffer.ToArray();
    }

    private static void Run(string file, params string[] args)
    {
        using var process = Start(file, args, redirectStdout: false);
        process?.WaitForExit(15_000);
    }

    private static Process? Start(string file, string[] args, bool redirectStdout)
    {
        var info = new ProcessStartInfo
        {
            FileName = file,
            RedirectStandardOutput = redirectStdout,
            UseShellExecute = false,
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return Process.Start(info);
    }
}
