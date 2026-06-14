using FluentAssertions;
using Moq;
using WebApp.Client.Features;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;
using static Farkle.Contracts.HttpResponses;

namespace Farkle.SpaTests.Handlers.RemoteTableChanged;

// #158 — covers WebApp.Client/Features/Actions/RemoteTableChanged.cs.
// Off-turn players watch the in-turn player's rolls/keeps live via a TableChanged snapshot.
public class HandleShould : HandlerTestContext
{
  private static GameStateResponse Snapshot(
    int currentPlayerId = 2,
    int turnScore = 150,
    IReadOnlyList<int>? center = null,
    IReadOnlyList<int>? kept = null) =>
    new(
      GameId:          0,
      Stage:           "Keeping",
      CurrentPlayerId: currentPlayerId,
      HostPlayerId:    1,
      TurnScore:       turnScore,
      Scoreboard:      [new PlayerScore(1, "Alice", 0), new PlayerScore(2, "Bob", 150)],
      Winner:          null,
      TableCenter:     center ?? [2, 3, 4],
      DiceKept:        kept ?? [1, 5]);

  // Seeds our identity (PlayerId) via RestoreGameState, which reads a mocked snapshot.
  private void SeedIdentity(int playerId, int currentPlayerId)
  {
    Mock.Get(GameService)
      .Setup(s => s.GetGameStateAsync(It.IsAny<int>()))
      .ReturnsAsync(Snapshot(currentPlayerId: currentPlayerId, center: [], kept: []));
  }

  [Fact]
  public async Task RebuildTheTable_ForOffTurnSpectators()
  {
    // We are player 1; it's player 2's turn → we are a spectator.
    SeedIdentity(playerId: 1, currentPlayerId: 2);
    await Sender.Send(new GameState.RestoreGameState.Action(1, "Alice"));

    await Sender.Send(new GameState.RemoteTableChanged.Action(Snapshot()));

    State.CurrentPlayerId.Should().Be(2);
    State.TurnScore.Value.Should().Be(150);
    State.DiceInPlay.Where(d => d.Identifier == "Rolled").Select(d => (int)d.Value)
      .Should().Equal(2, 3, 4);
    State.DiceInPlay.Where(d => d.Identifier == "SetAside").Select(d => (int)d.Value)
      .Should().Equal(1, 5);
    State.DiceInPlay.Should().OnlyContain(d => !d.Animate, "spectators see a settled snapshot");
    State.Scoreboard.Single(s => s.Name == "Bob").Score.Should().Be(150);
  }

  [Fact]
  public async Task IgnoreTheSnapshot_WhenItIsMyTurn()
  {
    // We are player 2 and it's our turn — our local drag state must not be clobbered.
    SeedIdentity(playerId: 2, currentPlayerId: 2);
    await Sender.Send(new GameState.RestoreGameState.Action(2, "Bob"));
    State.CurrentPlayerId = 2;
    State.DiceInPlay.Clear();
    State.DiceInPlay.Add(new DraggableDie(0, DieValue.Six, "SetAside"));

    await Sender.Send(new GameState.RemoteTableChanged.Action(Snapshot(currentPlayerId: 2)));

    State.DiceInPlay.Should().ContainSingle()
      .Which.Value.Should().Be(DieValue.Six);
  }
}
