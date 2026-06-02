using System.Text.Json;
using System.Text.RegularExpressions;

namespace Farkle.E2eTests;

/// <summary>
/// Shared Playwright helpers that drive a player through the opening stages of a game
/// (landing → start/join → roll → drag). Extracted from the happy-path test so the
/// infra-light storyboard capture can reuse the exact same flow against a different
/// (in-memory) backend without duplicating the selectors/timing logic.
/// </summary>
internal static class GameFlow
{
    public const int WasmTimeoutMs = 120_000;

    // Loads the landing page and waits for WASM hydration (the Start button appears).
    // Full-page navigation (GotoAsync) restarts WASM, giving a fresh BlazorState store
    // with no player-specific data from a previous game session.
    public static async Task GotoLandingAndWaitForWasmAsync(IPage page)
    {
        // WaitUntilState.Commit fires as soon as the server sends response headers,
        // before any CSS or JS is fetched. This bypasses the render-blocking
        // fonts.googleapis.com link that causes GotoAsync to time out waiting for Load.
        await page.GotoAsync("/", new() { WaitUntil = WaitUntilState.Commit });
        await page.WaitForSelectorAsync("[data-testid='start-new-game']",
            new() { Timeout = WasmTimeoutMs });
    }

    // Hosts a new game from the landing page: enters a name, clicks "Start New Game",
    // waits for navigation to /games/{id}, and returns the server-generated id.
    // Assumes the landing page is already loaded (see GotoLandingAndWaitForWasmAsync) so
    // a caller may screenshot the landing frame before this interaction.
    public static async Task<int> StartNewGameAsync(IPage page, string playerName)
    {
        // The Start card's name field is the first 'Your name' input.
        await page.Locator("[placeholder='Your name']").First.FillAsync(playerName);
        // Only click once the bind has enabled the button (avoids a default-timeout
        // wait if the value hasn't propagated yet).
        await page.WaitForSelectorAsync("[data-testid='start-new-game']:not([disabled])",
            new() { Timeout = WasmTimeoutMs });
        await page.ClickAsync("[data-testid='start-new-game']");
        await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
        var match = Regex.Match(page.Url, @"/games/(\d+)");
        return int.Parse(match.Groups[1].Value);
    }

    // Loads the landing page and hosts a new game in one step.
    public static async Task<int> StartNewGameFromLandingAsync(IPage page, string playerName)
    {
        await GotoLandingAndWaitForWasmAsync(page);
        return await StartNewGameAsync(page, playerName);
    }

    // Joins an existing game from the landing page using its share code.
    public static async Task JoinExistingGameFromLandingAsync(IPage page, string playerName, int gameId)
    {
        await GotoLandingAndWaitForWasmAsync(page);
        // The Join card's name field is the second 'Your name' input.
        await page.Locator("[placeholder='Your name']").Last.FillAsync(playerName);
        await page.FillAsync("[placeholder='Game code']", gameId.ToString());
        await page.WaitForSelectorAsync("[data-testid='join-existing-game']:not([disabled])",
            new() { Timeout = WasmTimeoutMs });
        await page.ClickAsync("[data-testid='join-existing-game']");
        await page.WaitForURLAsync(new Regex(@"/games/\d+"), new() { Timeout = WasmTimeoutMs });
    }

    // MudBlazor's MudDropZone uses HTML5 drag events. Playwright's DragToAsync fires
    // mouse events which don't reliably trigger the HTML5 drag API in headless Chrome,
    // so we dispatch the events directly via JS.
    public static Task DragDieAsync(IPage page, int index) =>
        page.EvaluateAsync($@"() => {{
            const dice   = document.querySelectorAll('.mud-drop-zone')[0]
                                   .querySelectorAll('.mud-drop-item-draggable');
            const source = dice[{index}];
            const target = document.querySelectorAll('.mud-drop-zone')[1];
            const dt     = new DataTransfer();
            source.dispatchEvent(new DragEvent('dragstart', {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
            target.dispatchEvent(new DragEvent('dragenter', {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
            target.dispatchEvent(new DragEvent('dragover',  {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
            target.dispatchEvent(new DragEvent('drop',      {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
            source.dispatchEvent(new DragEvent('dragend',   {{ bubbles: true, cancelable: true, dataTransfer: dt }}));
        }}");

    public static int[] ParseDiceValues(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("diceValues")
            .EnumerateArray()
            .Select(v => v.GetInt32())
            .ToArray();
    }
}
