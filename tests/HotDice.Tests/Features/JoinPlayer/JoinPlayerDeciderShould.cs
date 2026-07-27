using HotDice.Domain.GameAggregate;
using HotDice.Features.JoinPlayer;
using HotDice.SharedKernel.Turns;
using FluentAssertions;
using static HotDice.Domain.GameAggregate.GameEvents;

namespace HotDice.Tests.Features.JoinPlayer;

public class JoinPlayerDeciderShould
{
  [Fact]
  public void EmitPlayerJoinedWithTheNextSequentialId()
  {
    var lobby = GameState.Fold(new V1.GameStarted(1));

    var events = JoinPlayerDecider.Decide(new JoinPlayerCommand(1, "Alice"), lobby);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)));
  }

  [Fact]
  public void AssignIdsByJoinOrder()
  {
    var lobbyWithOne = GameState.Fold(
      new V1.GameStarted(1),
      new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)));

    var events = JoinPlayerDecider.Decide(new JoinPlayerCommand(1, "Bob"), lobbyWithOne);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V2.PlayerJoined(2, "Bob", PlayerColors.For(2)));
  }

  [Fact]
  public void RejectJoiningBeforeTheLobbyIsOpen()
  {
    var events = JoinPlayerDecider.Decide(new JoinPlayerCommand(1, "Alice"), new GameState());

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new GameHasNotStarted(GameStage.None));
  }
}
