using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Farkle.E2eTests;

/// <summary>
/// #269 — the navigation drawer must be toggleable on mobile. Before the fix the layout rendered
/// statically, so the hamburger's OnClick never ran and the drawer was stuck (unreachable menu on
/// phones). This drives a real browser at a phone viewport and asserts the toggle actually flips
/// the MudBlazor drawer open/closed — bUnit can't catch it (it always renders interactively).
/// In-memory backend (Storyboard collection), so no Testcontainers/Docker.
/// </summary>
[Collection(StoryboardCollection.Name)]
[Trait("Category", "Storyboard")]
public class NavDrawerShould(StoryboardFixture fixture)
{
    [Fact]
    public async Task ToggleOpenAndClosedFromTheHamburgerOnMobile()
    {
        var context = await fixture.NewContextAsync(width: 390, height: 844); // phone portrait
        var page = await context.NewPageAsync();
        try
        {
            await GameFlow.GotoLandingAndWaitForWasmAsync(page);

            var drawer = page.Locator(".mud-drawer");
            var hamburger = page.Locator("button[aria-label='Toggle navigation menu']");
            await hamburger.WaitForAsync(new() { Timeout = GameFlow.WasmTimeoutMs });

            // On a phone the responsive drawer starts closed — the hamburger is the only way in.
            await Expect(drawer).ToHaveClassAsync(new Regex(@"mud-drawer--closed"));

            // Tap → opens, revealing the menu (only works when the layout is interactive).
            await hamburger.ClickAsync();
            await Expect(drawer).ToHaveClassAsync(new Regex(@"mud-drawer--open"));
            (await page.GetByRole(AriaRole.Link, new() { Name = "Scoring" }).IsVisibleAsync())
                .Should().BeTrue("the Scoring nav link is reachable once the drawer is open");

            // On mobile the open drawer is a temporary overlay; tap its scrim (on the right, clear
            // of the left-side drawer) to close it.
            await page.Locator(".mud-overlay").ClickAsync(new() { Position = new() { X = 370, Y = 420 } });
            await Expect(drawer).ToHaveClassAsync(new Regex(@"mud-drawer--closed"));
        }
        finally
        {
            await context.CloseAsync();
        }
    }
}
