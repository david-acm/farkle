using System.Text.RegularExpressions;
using FluentAssertions;
using OpenQA.Selenium.Appium;
using Xunit;

namespace HotDice.DeviceTests;

/// <summary>
/// The one on-device happy path (#339, ADR 0011), driven through the WebView context with Appium.
/// It mirrors <c>GameHappyPathShould.HappyPath</c>'s selectors but is intentionally SHORTER than a
/// full game-to-win: the device tier proves only what it uniquely can (ADR 0010) —
///
///   launch (no blank WebView) → enter a name → start a game → a SignalR-pushed lobby update →
///   roll → set a die aside → keep (best-effort, see below).
///
/// It skips (not fails) when no device is configured, so it is inert outside the device workflow.
///
/// Die faces are not in the DOM (a 3-D CSS cube) and Appium can't intercept the /rolls response the
/// way Playwright does, so Keep needs a guaranteed scoring die. The gate boots the backend with
/// Dice:Scripted=true (every roll is six 1s), so slot 0 is always a scoring die and Keep is a firm,
/// asserted step rather than best-effort.
/// </summary>
public sealed partial class DeviceHappyPathShould
{
    [SkippableFact]
    public async Task DriveTheOpeningFlowOnDevice()
    {
        Skip.IfNot(UITestConfig.IsConfigured,
            "No device configured (set UITEST_PLATFORM + UITEST_APP_PATH — see the README).");

        var config = UITestConfig.FromEnvironment();
        using var backend = new BackendClient(config.BackendUri);

        var driver = DeviceDriver.Create(config);
        var evidence = new Evidence(driver, config);
        try
        {
            // ── 1. Cold launch: the WebView must render the real landing page, not a blank ──
            driver.SwitchToWebView();
            driver.WaitForVisible(Selectors.StartNewGame, DeviceDriver.HydrationTimeout)
                .Should().NotBeNull("a cold launch must render the game UI, not a blank WebView");
            evidence.Capture("launch");

            // ── 2. Enter a name and host a game ──
            driver.FindElements(Selectors.NameInput).First().SendKeys("Alice");
            driver.WaitForEnabled(Selectors.StartNewGame).Click();

            // Detect the game via the DOM, not the WebView URL: Blazor Hybrid drives navigation
            // through the router without changing the document URL, so a URL-based wait never
            // resolves on-device. The lobby appearing also proves the backend round-trip (create
            // game + host auto-join) actually completed.
            driver.WaitForVisible(Selectors.Lobby, DeviceDriver.HydrationTimeout);
            var gameId = ParseGameId(driver.FindElement(Selectors.ShareGameId).Text);
            driver.Count(Selectors.RosterPlayer).Should().Be(1, "the host auto-joins the lobby");
            evidence.Capture("lobby");

            // ── 3. A SignalR-pushed update: a second player joins from OUTSIDE the device ──
            await backend.JoinAsync(gameId, "Bob");
            WaitForRosterCount(driver, 2)
                .Should().BeTrue("the lobby roster should grow to 2 via a SignalR LobbyChanged push, no interaction");
            evidence.Capture("signalr-join");

            // ── 4. Start the game (enabled once the second player arrived) ──
            driver.WaitForEnabled(Selectors.StartGameButton).Click();
            driver.WaitForVisible(Selectors.MyTurnIndicator)
                .Should().NotBeNull("the host is in turn once the game begins");
            evidence.Capture("in-turn");

            // ── 5. Roll ──
            driver.WaitForEnabled(Selectors.RollButton).Click();
            driver.WaitForVisible(Selectors.DieContainer)
                .Should().NotBeNull("rolling puts dice on the table");
            evidence.Capture("rolled");

            // ── 6. Set a scoring die aside (scripted dice guarantee a 1 in slot 0) ──
            driver.WaitForVisible(Selectors.TrayDie).Click();
            driver.WaitForVisible(Selectors.SelectedTrayDie)
                .Should().NotBeNull("tapping a die selects it (set-aside)");
            evidence.Capture("set-aside");

            // ── 7. Keep the selected die and confirm the turn score banked it ──
            driver.WaitForEnabled(Selectors.KeepButton).Click();
            driver.WaitForVisible(Selectors.TurnScore);
            driver.FindElement(Selectors.TurnScore).Text.Trim()
                .Should().NotBe("0", "keeping a scoring die banks it into the turn score");
            evidence.Capture("kept");
        }
        catch
        {
            // Dump the WebView DOM alongside the screenshot — in a headless emulator the page source
            // is the most reliable evidence of where the flow actually stopped (e.g. an error modal
            // vs. a still-loading page vs. a backend that never answered).
            evidence.Capture("failure");
            evidence.CapturePageSource("failure");
            throw;
        }
        finally
        {
            driver.Quit();
            driver.Dispose();
        }
    }

    private static bool WaitForRosterCount(AppiumDriver driver, int expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (driver.Count(Selectors.RosterPlayer) >= expected) return true;
            Thread.Sleep(300);
        }
        return false;
    }

    private static int ParseGameId(string shareText)
    {
        var match = GameIdDigits().Match(shareText);
        if (!match.Success)
            throw new InvalidOperationException($"No game id found in the lobby share text: '{shareText}'.");
        return int.Parse(match.Value);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex GameIdDigits();
}
