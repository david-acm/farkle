using FluentAssertions;
using Moq;
using WebApp.Client.Features;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.SpaTests.Handlers.Lobby;

// Covers the lobby action handlers: BeginGame, RemotePlayerJoined, RemoteGameBegan.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task BeginGame_MovesOutOfLobbyIntoRolling_AndSeedsScoreboard()
  {
    Mock.Get(GameService)
      .Setup(s => s.BeginGameAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(new LobbyStateResponse(
        GameId: 7,
        Stage: "Rolling",
        Roster: [new LobbyPlayer(1, "Alice"), new LobbyPlayer(2, "Bob")],
        HostPlayerId: 1,
        CurrentPlayerId: 1));

    await Sender.Send(new GameState.BeginGame.Action());

    State.GameStage.Should().Be("Rolling");
    State.IsInLobby.Should().BeFalse();
    State.CurrentPlayerId.Should().Be(1);
    State.Scoreboard.Should().HaveCount(2);
  }

  [Fact]
  public async Task RemotePlayerJoined_RefreshesRosterAndHost()
  {
    var payload = new LobbyStateResponse(
      GameId: 7,
      Stage: "WaitingForPlayers",
      Roster: [new LobbyPlayer(1, "Alice"), new LobbyPlayer(2, "Bob")],
      HostPlayerId: 1,
      CurrentPlayerId: 1);

    await Sender.Send(new GameState.RemotePlayerJoined.Action(payload));

    State.GameStage.Should().Be("WaitingForPlayers");
    State.HostPlayerId.Should().Be(1);
    State.Roster.Should().HaveCount(2);
    State.Roster.Select(p => p.Name).Should().Contain(["Alice", "Bob"]);
  }

  [Fact]
  public async Task RemoteGameBegan_FlipsToRolling_ForNonHostPlayers()
  {
    var payload = new LobbyStateResponse(
      GameId: 7,
      Stage: "Rolling",
      Roster: [new LobbyPlayer(1, "Alice"), new LobbyPlayer(2, "Bob")],
      HostPlayerId: 1,
      CurrentPlayerId: 1);

    await Sender.Send(new GameState.RemoteGameBegan.Action(payload));

    State.GameStage.Should().Be("Rolling");
    State.IsInLobby.Should().BeFalse();
    State.CurrentPlayerId.Should().Be(1);
    State.Scoreboard.Should().HaveCount(2);
  }
}
