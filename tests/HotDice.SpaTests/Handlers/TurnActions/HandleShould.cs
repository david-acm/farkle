using Ardalis.Result;
using Blazor.Dice;
using HotDice.SharedKernel.Turns;
using FluentAssertions;
using Moq;
using HotDice.Ui.Features;
using HotDice.Ui.Services;
using static HotDice.Contracts.HttpResponses;

namespace HotDice.SpaTests.Handlers.TurnActions;

// #286 — the client tracks the turn stage locally in its BlazorState handlers (roll →
// AwaitingKeep, keep → AwaitingRoll, pass / remote turn change → reset) so it can gate the
// Roll / Keep / Pass buttons through the shared TurnActionPolicy. No API contract change.
public class HandleShould : HandlerTestContext
{
  private async Task JoinAsMyTurn()
  {
    Mock.Get(GameService)
      .Setup(s => s.JoinPlayerAsync(It.IsAny<int>(), It.IsAny<string>()))
      .ReturnsAsync(new JoinPlayerResponse(Id: 1, CurrentPlayerId: 1));
    await Sender.Send(new GameState.JoinPlayer.Action(new PlayerName("Alice")));
  }

  [Fact]
  public async Task StartTheTurnAwaitingARoll()
  {
    await JoinAsMyTurn();

    State.PlayStage.Should().Be(GameStage.Rolling);
    State.HasActedThisTurn.Should().BeFalse();
    State.CanRoll.Should().BeTrue();
    State.CanPass.Should().BeFalse("you cannot pass before rolling");
    State.CanKeep.Should().BeFalse();
  }

  [Fact]
  public async Task MoveToAwaitingKeepAfterARoll()
  {
    await JoinAsMyTurn();
    Mock.Get(GameService)
      .Setup(s => s.RollDiceAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(Result<IList<DieValue>>.Success(new List<DieValue> { DieValue.Five }));

    await Sender.Send(new GameState.RollDice.Action());

    State.PlayStage.Should().Be(GameStage.Keeping);
    State.HasActedThisTurn.Should().BeTrue();
    State.CanRoll.Should().BeFalse("no second roll before a keep");
    State.CanPass.Should().BeTrue("you may pass once you have rolled");
  }

  [Fact]
  public async Task ReturnToAwaitingRollAfterAKeep()
  {
    await JoinAsMyTurn();
    Mock.Get(GameService)
      .Setup(s => s.RollDiceAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(Result<IList<DieValue>>.Success(new List<DieValue> { DieValue.Five }));
    Mock.Get(GameService)
      .Setup(s => s.KeepDiceAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
      .ReturnsAsync(new KeepDiceResponse(0, 50));

    await Sender.Send(new GameState.RollDice.Action());
    await Sender.Send(new GameState.KeepDice.Action());

    State.PlayStage.Should().Be(GameStage.Rolling);
    State.HasActedThisTurn.Should().BeTrue();
    State.CanRoll.Should().BeTrue("after keeping you may roll again");
    State.CanPass.Should().BeTrue("after keeping you may pass");
  }

  [Fact]
  public async Task ResetTheTurnOnPass()
  {
    await JoinAsMyTurn();
    Mock.Get(GameService)
      .Setup(s => s.RollDiceAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(Result<IList<DieValue>>.Success(new List<DieValue> { DieValue.Five }));
    Mock.Get(GameService)
      .Setup(s => s.PassTurnAsync(It.IsAny<int>(), It.IsAny<int>()))
      .ReturnsAsync(new PassTurnResponse(GameId: 0, PlayerId: 1, NewScore: 0, Winner: null, CurrentPlayerId: 1));

    await Sender.Send(new GameState.RollDice.Action());
    await Sender.Send(new GameState.PassTurn.Action());

    State.PlayStage.Should().Be(GameStage.Rolling);
    State.HasActedThisTurn.Should().BeFalse("a fresh turn has no committed action yet");
    State.CanPass.Should().BeFalse();
  }
}
