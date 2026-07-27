namespace HotDice.Ui.Services;

/// <summary>
/// Builds the deep-link URL another player can open (or scan from a QR code) to
/// join a game: <c>{baseUri}/games/{gameId}</c>.
/// </summary>
public static class JoinLink
{
  public static string For(string baseUri, int gameId) =>
    $"{baseUri.TrimEnd('/')}/games/{gameId}";
}
