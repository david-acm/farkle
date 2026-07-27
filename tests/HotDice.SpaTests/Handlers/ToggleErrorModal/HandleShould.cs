using FluentAssertions;
using HotDice.Ui.Features;

namespace HotDice.SpaTests.Handlers.ToggleErrorModal;

// H4 (state-contract half) — covers HotDice.Ui/Features/Actions/ToggleErrorModal.cs.
// Pinned so future work that wires ErrorModal into a page (currently absent
// from any layout/page — see PR notes) has a stable contract to rely on.
public class HandleShould : HandlerTestContext
{
  [Fact]
  public async Task FlipShowErrorAndStoreMessage_WhenOpening()
  {
    State.ShowError.Should().BeFalse("baseline");

    await Sender.Send(new GameState.ToggleErrorModal.Action(Show: true, ErrorMessage: "Boom"));

    State.ShowError.Should().BeTrue();
    State.ErrorMessage.Should().Be("Boom");
  }

  [Fact]
  public async Task LeaveMessageUnchanged_WhenClosingWithoutMessage()
  {
    await Sender.Send(new GameState.ToggleErrorModal.Action(Show: true, ErrorMessage: "Boom"));
    await Sender.Send(new GameState.ToggleErrorModal.Action(Show: false));

    State.ShowError.Should().BeFalse();
    State.ErrorMessage.Should().Be("Boom",
      "closing the modal shouldn't blank the message — the next open might show the same one");
  }
}
