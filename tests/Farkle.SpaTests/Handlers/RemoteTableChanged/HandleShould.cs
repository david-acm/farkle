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
    IReadOnlyList<int>? kept = null,
    IReadOnlyList<int>? setAside = null) =>
    new(
      GameId:          0,
      Stage:           "Keeping",
      CurrentPlayerId: currentPlayerId,
      HostPlayerId:    1,
      TurnScore:       turnScore,
      Scoreboard:      [new PlayerScore(1, "Alice", 0), new PlayerScore(2, "Bob", 150)],
      Winner:          null,
      TableCenter:     center ?? [2, 3, 4],
      DiceKept:        kept ?? [],
      DiceSetAside:    setAside ?? []);

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

    // Set-aside is a non-destructive overlay: the centre still contains the set-aside dice,
    // so the spectator's "Rolled" zone is centre minus selection, "SetAside" is the selection.
    await Sender.Send(new GameState.RemoteTableChanged.Action(
      Snapshot(center: [1, 2, 3, 4, 5], setAside: [1, 5])));

    State.CurrentPlayerId.Should().Be(2);
    State.TurnScore.Value.Should().Be(150);
    State.DiceInPlay.Where(d => !d.IsSelected).Select(d => (int)d.Value)
      .Should().Equal(2, 3, 4);
    State.DiceInPlay.Where(d => d.IsSelected).Select(d => (int)d.Value)
      .Should().Equal(1, 5);
    State.DiceInPlay.Should().OnlyContain(d => !d.IsAnimated, "spectators see a settled snapshot");
    State.Scoreboard.Single(s => s.Name == "Bob").Score.Should().Be(150);
  }

  [Fact]
  public async Task KeepDiceInStableTableOrder_WhenADieIsSetAside()
  {
    // #186 — a set-aside must only flip the die's highlight, not reorder the board. The
    // dice stay in TableCenter order at their original index (so the grid never relocates
    // dice or re-spins them); the selected die is marked in place, not moved to the end.
    SeedIdentity(playerId: 1, currentPlayerId: 2);
    await Sender.Send(new GameState.RestoreGameState.Action(1, "Alice"));

    await Sender.Send(new GameState.RemoteTableChanged.Action(
      Snapshot(center: [1, 2, 3, 4, 5, 6], setAside: [3])));

    State.DiceInPlay.Select(d => (int)d.Value).Should().Equal(1, 2, 3, 4, 5, 6);
    State.DiceInPlay.Select(d => d.Index).Should().Equal(0, 1, 2, 3, 4, 5);
    var three = State.DiceInPlay.Single(d => (int)d.Value == 3);
    three.IsSelected.Should().BeTrue();
    three.Index.Should().Be(2, "the selected die stays in its original position");
    State.DiceInPlay.Should().OnlyContain(d => !d.IsAnimated);
  }

  [Fact]
  public async Task KeepSurvivorsAtTheirOriginalIndex_WhenTheInTurnPlayerKeeps()
  {
    // #204 — a keep removes the kept dice from TableCenter, so the snapshot shrinks and
    // re-indexes. If survivors are re-indexed from 0, every die *after* a kept one gets a new
    // @key and Blazor re-mounts its Die — which replays the mount spin for the spectator.
    // Survivors must keep their ORIGINAL table position as Index (stable @key → reused
    // component → no re-spin), mirroring how the in-turn player's KeepDice retains its dice.
    SeedIdentity(playerId: 1, currentPlayerId: 2);
    await Sender.Send(new GameState.RestoreGameState.Action(1, "Alice"));

    // Roll: six dice at indices 0..5.
    await Sender.Send(new GameState.RemoteTableChanged.Action(
      Snapshot(center: [1, 2, 3, 4, 5, 6]), Animate: true));
    State.DiceInPlay.Select(d => d.Index).Should().Equal(0, 1, 2, 3, 4, 5);

    // The in-turn player keeps the 2 and 4 → TableCenter shrinks to [1, 3, 5, 6].
    await Sender.Send(new GameState.RemoteTableChanged.Action(Snapshot(center: [1, 3, 5, 6])));

    State.DiceInPlay.Select(d => (int)d.Value).Should().Equal(1, 3, 5, 6);
    State.DiceInPlay.Select(d => d.Index).Should().Equal(
      new[] { 0, 2, 4, 5 },
      "survivors keep their original positions so @key stays stable (no re-mount / re-spin, #204)");
    State.DiceInPlay.Should().OnlyContain(d => !d.IsAnimated, "a keep is not a roll");
  }

  [Fact]
  public async Task AnimateRolledDice_OnARollSnapshot()
  {
    // #167 — a roll snapshot (Animate: true) spins the in-play dice for spectators too.
    SeedIdentity(playerId: 1, currentPlayerId: 2);
    await Sender.Send(new GameState.RestoreGameState.Action(1, "Alice"));

    await Sender.Send(new GameState.RemoteTableChanged.Action(
      Snapshot(center: [1, 2, 3, 4, 5, 6], setAside: []), Animate: true));

    State.DiceInPlay.Where(d => !d.IsSelected)
      .Should().OnlyContain(d => d.IsAnimated, "a roll spins for spectators");
  }

  [Fact]
  public async Task IgnoreTheSnapshot_WhenItIsMyTurn()
  {
    // We are player 2 and it's our turn — our local selection state must not be clobbered.
    SeedIdentity(playerId: 2, currentPlayerId: 2);
    await Sender.Send(new GameState.RestoreGameState.Action(2, "Bob"));
    State.CurrentPlayerId = 2;
    State.DiceInPlay.Clear();
    State.DiceInPlay.Add(TrayDie.SetAside(0, DieValue.Six));

    await Sender.Send(new GameState.RemoteTableChanged.Action(Snapshot(currentPlayerId: 2)));

    State.DiceInPlay.Should().ContainSingle()
      .Which.Value.Should().Be(DieValue.Six);
  }
}
