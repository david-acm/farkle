namespace HotDice.DeviceTests;

/// <summary>
/// Every runner-specific value the device tests need, read from <c>UITEST_*</c> environment
/// variables so the exact same test binary runs locally (against a booted simulator/emulator)
/// and in CI. This mirrors the repo's existing "config via env var" precedents —
/// <c>HOTDICE_BACKEND_URL</c> (the shell), <c>E2E_STEP_DELAY_MS</c> (Playwright), and
/// <c>PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH</c> (remote sessions).
///
/// When <see cref="Platform"/> is unset the suite <b>skips</b> (see <see cref="IsConfigured"/>),
/// so a device test never fails merely because no device is attached.
/// </summary>
public sealed record UITestConfig
{
    public enum DevicePlatform { Android, IOS }

    /// <summary>android | ios — the only required variable; unset ⇒ the suite skips.</summary>
    public required DevicePlatform Platform { get; init; }

    /// <summary>The Appium server URL. Defaults to the local server's conventional address.</summary>
    public required Uri AppiumServerUri { get; init; }

    /// <summary>Absolute path to the built app artifact (.apk/.aab on Android, .app/.ipa on iOS).</summary>
    public required string AppPath { get; init; }

    /// <summary>
    /// The UDID/serial of the target device. Pinning this is load-bearing: an unpinned run can
    /// attach to (or record) the wrong simulator when more than one is booted (see the runbook).
    /// </summary>
    public string? Udid { get; init; }

    /// <summary>
    /// The backend the app talks to. Baked into the app build in CI; surfaced here only so the
    /// background "second player" join (the SignalR-push observation) hits the same backend.
    /// </summary>
    public required Uri BackendUri { get; init; }

    /// <summary>Directory for per-state screenshots + any captured logs.</summary>
    public required string DiagnosticsDir { get; init; }

    /// <summary>Overall app-bundle id, needed by XCUITest to (re)launch on iOS.</summary>
    public string BundleId { get; init; } = "com.davidacm.hotdice";

    /// <summary>
    /// Path to the <c>adb</c> binary for device-level screenshots. The wrapper passes the resolved
    /// absolute path (a bare "adb" isn't always on the test process's PATH); defaults to "adb".
    /// </summary>
    public string AdbPath { get; init; } = "adb";

    public bool IsAndroid => Platform == DevicePlatform.Android;
    public bool IsIos => Platform == DevicePlatform.IOS;

    /// <summary>True only when UITEST_PLATFORM is set to a recognised value.</summary>
    public static bool IsConfigured =>
        TryParsePlatform(Environment.GetEnvironmentVariable("UITEST_PLATFORM"), out _);

    /// <summary>Reads the environment into a config. Only call when <see cref="IsConfigured"/>.</summary>
    public static UITestConfig FromEnvironment()
    {
        if (!TryParsePlatform(Environment.GetEnvironmentVariable("UITEST_PLATFORM"), out var platform))
            throw new InvalidOperationException(
                "UITEST_PLATFORM is not set to 'android' or 'ios'. Guard with UITestConfig.IsConfigured.");

        var appPath = Require("UITEST_APP_PATH");
        var diagDir = Environment.GetEnvironmentVariable("UITEST_DIAG_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "diagnostics");
        Directory.CreateDirectory(diagDir);

        return new UITestConfig
        {
            Platform        = platform,
            AppiumServerUri = new Uri(Environment.GetEnvironmentVariable("UITEST_APPIUM_URL")
                                      ?? "http://127.0.0.1:4723/"),
            AppPath         = appPath,
            Udid            = Environment.GetEnvironmentVariable("UITEST_UDID"),
            BackendUri      = new Uri(Environment.GetEnvironmentVariable("UITEST_BACKEND_URL")
                                      ?? "http://10.0.2.2:5000/"),
            DiagnosticsDir  = diagDir,
            BundleId        = Environment.GetEnvironmentVariable("UITEST_BUNDLE_ID")
                              ?? "com.davidacm.hotdice",
            AdbPath         = Environment.GetEnvironmentVariable("UITEST_ADB") is { Length: > 0 } adb
                              ? adb : "adb",
        };
    }

    private static string Require(string name) =>
        Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} must be set for a device run (see the README).");

    private static bool TryParsePlatform(string? value, out DevicePlatform platform)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "android": platform = DevicePlatform.Android; return true;
            case "ios":     platform = DevicePlatform.IOS;     return true;
            default:        platform = default;                return false;
        }
    }
}
