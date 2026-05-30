using FluentAssertions;
using Moq;
using WebApp.Client.Features;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.SpaTests.Handlers.RestoreGameState;

// Covers WebApp.Client/Features/Actions/RestoreGameState.cs — re-hydrating the
// store from a server snapshot + the session-stored identity on refresh/reconnect.
public class HandleShould : HandlerTestContext
{
  private static GameStateResponse Snapshot(int currentPlayerId, string stage = "Keeping") =>
    new(
      GameId:          42,
      Stage:           stage,
      CurrentPlayerId: currentPlayerId,
      HostPlayerId:    1,
      TurnScore:       150,
      Scoreboard:      [new PlayerScore(1, "Host", 500), new PlayerScore(7, "Alice", 150)],
      Winner:          null,
      TableCenter:     [1, 2, 3],
      DiceKept:        [5, 5]);

  [Fact]
  public async Task RestoreIdentityScoreboardAndDice_WhenItIsMyTurn()
  {
    Mock.Get(GameService)
      .Setup(s => s.GetGameStateAsync(It.IsAny<int>()))
      .ReturnsAsync(Snapshot(currentPlayerId: 7));

    await Sender.Send(new GameState.RestoreGameState.Action(PlayerId: 7, PlayerName: "Alice"));

    State.GameId.Value.Should().Be(42);
    State.PlayerId.Value.Should().Be(7);
    State.PlayerName.Value.Should().Be("Alice");
    State.CurrentPlayerId.Should().Be(7);
    State.IsMyTurn.Should().BeTrue();
    State.GameStage.Should().Be("Keeping");
    State.HostPlayerId.Should().Be(1);
    State.WinnerName.Should().BeNull();

    State.Scoreboard.Should().HaveCount(2);
    State.Scoreboard.Single(s => s.Name == "Alice").Score.Should().Be(150);
    State.Roster.Should().HaveCount(2);

    // The active player's turn resumes: their dice are back on the table.
    State.TurnScore.Value.Should().Be(150);
    State.DiceInPlay.Should().HaveCount(3);
    State.DiceInPlay.Select(d => d.Value).Should().Equal(DieValue.One, DieValue.Two, DieValue.Three);
    State.DiceInPlay.Should().OnlyContain(d => d.Identifier == "Rolled");
    State.DiceInPlay.Select(d => d.Index).Should().Equal(0, 1, 2);
  }

  [Fact]
  public async Task RestoreBoardButNotDice_WhenItIsAnotherPlayersTurn()
  {
    Mock.Get(GameService)
      .Setup(s => s.GetGameStateAsync(It.IsAny<int>()))
      .ReturnsAsync(Snapshot(currentPlayerId: 7));

    // Restoring as the host (player 1), but it is Alice's (player 7) turn.
    await Sender.Send(new GameState.RestoreGameState.Action(PlayerId: 1, PlayerName: "Host"));

    State.PlayerId.Value.Should().Be(1);
    State.CurrentPlayerId.Should().Be(7);
    State.IsMyTurn.Should().BeFalse();
    State.GameStage.Should().Be("Keeping");
    State.Scoreboard.Should().HaveCount(2);

    // Non-active players never see the active player's dice in live play, so a refresh
    // must not surface them either.
    State.DiceInPlay.Should().BeEmpty();
    State.TurnScore.Value.Should().Be(0);
  }

  [Fact]
  public async Task SurfaceWinner_WhenGameIsOver()
  {
    Mock.Get(GameService)
      .Setup(s => s.GetGameStateAsync(It.IsAny<int>()))
      .ReturnsAsync(new GameStateResponse(
        GameId:          42,
        Stage:           "Finished",
        CurrentPlayerId: 7,
        HostPlayerId:    1,
        TurnScore:       0,
        Scoreboard:      [new PlayerScore(7, "Alice", 10_000)],
        Winner:          new WinnerResponse(7, "Alice", 10_000),
        TableCenter:     [],
        DiceKept:        []));

    await Sender.Send(new GameState.RestoreGameState.Action(PlayerId: 7, PlayerName: "Alice"));

    State.GameStage.Should().Be("Finished");
    State.WinnerName.Should().Be("Alice");
    State.IsGameOver.Should().BeTrue();
  }

  [Fact]
  public async Task LeaveStateUntouched_WhenGameNoLongerExists()
  {
    Mock.Get(GameService)
      .Setup(s => s.GetGameStateAsync(It.IsAny<int>()))
      .ReturnsAsync(default(GameStateResponse));

    await Sender.Send(new GameState.RestoreGameState.Action(PlayerId: 7, PlayerName: "Alice"));

    // No identity is set, so the page treats this as "not joined" and shows the join form.
    State.PlayerName.Value.Should().BeEmpty();
    State.GameId.Value.Should().Be(0);
  }
}
