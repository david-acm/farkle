using OpenQA.Selenium;

namespace HotDice.DeviceTests;

/// <summary>
/// The selector map for the game UI, located inside the WebView context. These are the SAME
/// <c>data-testid</c> hooks and button labels the Playwright suite drives
/// (<c>tests/HotDice.E2eTests/GameFlow.cs</c> + <c>GameHappyPathShould.cs</c>) — one source of
/// truth for "what a happy path clicks", shared across the web and device tiers. Button labels
/// (Roll/Keep/Pass Turn) are load-bearing UI text; do not rename them (see CLAUDE.md).
/// </summary>
internal static class Selectors
{
    public static By TestId(string id) => By.CssSelector($"[data-testid='{id}']");

    // Landing.
    public static readonly By StartNewGame     = TestId("start-new-game");
    public static readonly By JoinExistingGame = TestId("join-existing-game");
    public static By NameInput  => By.CssSelector("[placeholder='Your name']");
    public static By GameCode   => By.CssSelector("[placeholder='Game code']");

    // Lobby.
    public static readonly By Lobby           = TestId("lobby");
    public static readonly By ShareGameId     = TestId("share-game-id");
    public static readonly By RosterPlayer    = TestId("roster-player");
    public static readonly By StartGameButton = TestId("start-game-button");
    public static readonly By WaitingForHost  = TestId("waiting-for-host");

    // Play.
    public static readonly By MyTurnIndicator  = TestId("my-turn-indicator");
    public static readonly By WaitingIndicator = TestId("waiting-indicator");
    public static readonly By Scoreboard       = TestId("scoreboard");
    public static readonly By TurnScore        = TestId("turn-score");
    public static readonly By TrayDie          = TestId("tray-die");
    public static By DieContainer   => By.CssSelector(".die-container");
    public static By SelectedTrayDie => By.CssSelector(".dice-tray-die.selected");

    // Buttons are MudButtons with load-bearing visible text; match on the text inside a <button>.
    public static By ButtonWithText(string text) =>
        By.XPath($"//button[contains(normalize-space(.), '{text}')]");

    public static readonly By RollButton = ButtonWithText("Roll");
    public static readonly By KeepButton = ButtonWithText("Keep");
    public static readonly By PassButton = ButtonWithText("Pass Turn");
}
