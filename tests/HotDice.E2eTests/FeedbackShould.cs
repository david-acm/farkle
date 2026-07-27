using System.Runtime.ExceptionServices;
using Microsoft.Playwright;

namespace HotDice.E2eTests;

/// <summary>
/// #277 — the global feedback widget, end to end against the real backend:
/// a rapid-click burst pulses the icon and shows the hint (client-side only, no backend call),
/// then the user opens the form, writes feedback with a 👎, and submits it to POST /api/feedback.
/// Recorded as <c>Feedback.webm</c>.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class FeedbackShould(PlaywrightFixture fixture)
{
    private static string VideoDir =>
        Path.GetFullPath(Path.Join(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "test-results", "videos"));

    [Fact]
    public async Task PulseOnRageBurstThenSubmitFeedback()
    {
        var context = await fixture.NewContextWithVideoAsync(VideoDir);
        var page    = await context.NewPageAsync();

        Exception? failure = null;
        try
        {
            await GameFlow.GotoLandingAndWaitForWasmAsync(page);

            // The feedback icon is present on every page (rendered in MainLayout).
            await page.WaitForSelectorAsync("[data-testid='feedback-button']",
                new() { Timeout = GameFlow.WasmTimeoutMs });

            // A rapid-click burst (≥4 within 1s) pulses the icon and surfaces the hint — entirely
            // client-side (App Insights UI.RageClick), no backend call. Dispatch clicks on <body>
            // so they hit the document listener without toggling the form.
            await page.EvaluateAsync("() => { for (let i = 0; i < 6; i++) document.body.click(); }");
            await page.WaitForSelectorAsync("[data-testid='feedback-hint']", new() { Timeout = 5_000 });

            // Open the form, write feedback, mark it 👎, and submit.
            await page.ClickAsync("[data-testid='feedback-button']");
            await page.WaitForSelectorAsync("[data-testid='feedback-form']", new() { Timeout = 5_000 });
            await page.FillAsync("[data-testid='feedback-form'] textarea", "The landing page rocks");
            await page.ClickAsync("[data-testid='feedback-down']");
            await page.ClickAsync("[data-testid='feedback-submit']");

            // POST /api/feedback succeeded → the submission is acknowledged.
            await page.WaitForSelectorAsync("[data-testid='feedback-thanks']", new() { Timeout = 10_000 });
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await context.CloseAsync(); // finalise the .webm
            var raw = await page.Video!.PathAsync();
            if (File.Exists(raw))
                File.Move(raw, Path.Combine(VideoDir, "Feedback.webm"), overwrite: true);
        }

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
