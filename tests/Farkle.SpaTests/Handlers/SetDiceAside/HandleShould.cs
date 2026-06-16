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
    var die = new TrayDie(0, DieValue.Five, "Rolled");
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, "SetAside"));

    State.DiceInPlay[0].Identifier.Should().Be("SetAside");
  }

  [Fact]
  public async Task StopAnimatingTheMovedDie_SoItDoesNotLookReRolled()
  {
    // Dice spin only when rolled. A die starts animatable (just rolled); moving it
    // between zones must clear that flag so the Die renders its face without spinning.
    var die = new TrayDie(0, DieValue.Five, "Rolled") { Animate = true };
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, "SetAside"));

    State.DiceInPlay[0].Animate.Should().BeFalse("a move is not a roll");
  }

  [Fact]
  public async Task ResolveByIndex_NotByValue_WhenDuplicateFaces()
  {
    State.DiceInPlay.Add(new TrayDie(0, DieValue.Five, "Rolled"));
    State.DiceInPlay.Add(new TrayDie(1, DieValue.Five, "Rolled"));

    await Sender.Send(new GameState.SetDiceAside.Action(
      new TrayDie(1, DieValue.Five, "SetAside"), "SetAside"));

    State.DiceInPlay[0].Identifier.Should().Be("Rolled",  "first Five must remain in play");
    State.DiceInPlay[1].Identifier.Should().Be("SetAside", "only the targeted Five moves");
  }

  [Fact]
  public async Task SyncSetAsideToTheServer_WhenMovedIntoTheSetAsideZone()
  {
    // #159 — promoting the selection to a domain command so it persists and broadcasts.
    var die = new TrayDie(0, DieValue.Five, "Rolled");
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, "SetAside"));

    Mock.Get(GameService).Verify(
      s => s.SetDiceAsideAsync(It.IsAny<int>(), It.IsAny<int>(), 5), Times.Once);
    Mock.Get(GameService).Verify(
      s => s.ReturnDiceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
  }

  [Fact]
  public async Task SyncPutBackToTheServer_WhenMovedBackIntoPlay()
  {
    var die = new TrayDie(0, DieValue.Five, "SetAside");
    State.DiceInPlay.Add(die);

    await Sender.Send(new GameState.SetDiceAside.Action(die, "Rolled"));

    Mock.Get(GameService).Verify(
      s => s.ReturnDiceAsync(It.IsAny<int>(), It.IsAny<int>(), 5), Times.Once);
    Mock.Get(GameService).Verify(
      s => s.SetDiceAsideAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
  }
}
