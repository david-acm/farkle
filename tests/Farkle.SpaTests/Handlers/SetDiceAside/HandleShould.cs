using FluentAssertions;
using Moq;
using WebApp.Client.Features;
using WebApp.Client.Pages.Game.Components;
using WebApp.Client.Services;

namespace Farkle.SpaTests.Handlers.SetDiceAside;

// Covers WebApp.Client/Features/Actions/SetDiceAside.cs. The duplication and
// cross-turn-leak regressions live in DiceSetAsideStaleShould (sibling file);
// these tests pin the contract.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task FlipIdentifierToTargetZone()
  {
    var die = TrayDie.Rolled(0, DieValue.Five);
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, DiceZone.SetAside));

    State.DiceInPlay[0].IsSelected.Should().BeTrue();
  }

  [Fact]
  public async Task StopAnimatingTheMovedDie_SoItDoesNotLookReRolled()
  {
    // Dice spin only when rolled. A die starts animatable (just rolled); moving it
    // between zones must clear that flag so the Die renders its face without spinning.
    var die = TrayDie.Rolled(0, DieValue.Five);
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, DiceZone.SetAside));

    State.DiceInPlay[0].IsAnimated.Should().BeFalse("a move is not a roll");
  }

  [Fact]
  public async Task ResolveByIndex_NotByValue_WhenDuplicateFaces()
  {
    State.DiceInPlay.Add(TrayDie.Rolled(0, DieValue.Five));
    State.DiceInPlay.Add(TrayDie.Rolled(1, DieValue.Five));

    await Sender.Send(new GameState.SetDiceAside.Action(
      TrayDie.SetAside(1, DieValue.Five), DiceZone.SetAside));

    State.DiceInPlay[0].IsSelected.Should().BeFalse( "first Five must remain in play");
    State.DiceInPlay[1].IsSelected.Should().BeTrue("only the targeted Five moves");
  }

  [Fact]
  public async Task SyncSetAsideToTheServer_WhenMovedIntoTheSetAsideZone()
  {
    // #159 — promoting the selection to a domain command so it persists and broadcasts.
    var die = TrayDie.Rolled(0, DieValue.Five);
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, DiceZone.SetAside));

    Mock.Get(GameService).Verify(
      s => s.SetDiceAsideAsync(It.IsAny<int>(), It.IsAny<int>(), 5), Times.Once);
    Mock.Get(GameService).Verify(
      s => s.ReturnDiceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task SyncPutBackToTheServer_WhenMovedBackIntoPlay()
  {
    var die = TrayDie.SetAside(0, DieValue.Five);
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, DiceZone.Rolled));

    Mock.Get(GameService).Verify(
      s => s.ReturnDiceAsync(It.IsAny<int>(), It.IsAny<int>(), 5), Times.Once);
    Mock.Get(GameService).Verify(
      s => s.SetDiceAsideAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
  }
}
