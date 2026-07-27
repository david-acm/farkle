using FluentAssertions;
using HotDice.Ui.Services;
using Xunit;

namespace HotDice.SpaTests.Services;

// Issue #163 — the QR / share target is the deep-link join URL for a game.
public class JoinLinkShould
{
  [Theory]
  [InlineData("http://localhost/", 5, "http://localhost/games/5")]
  [InlineData("http://localhost", 5, "http://localhost/games/5")] // base without trailing slash
  [InlineData("https://farkle.example.com/", 42, "https://farkle.example.com/games/42")]
  public void BuildTheDeepLinkJoinUrl(string baseUri, int gameId, string expected) =>
    JoinLink.For(baseUri, gameId).Should().Be(expected);
}
