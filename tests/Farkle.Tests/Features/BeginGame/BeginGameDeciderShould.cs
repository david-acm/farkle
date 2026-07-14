using Farkle.Domain.GameAggregate;
using Farkle.Features.BeginGame;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Features.BeginGame;

public class BeginGameDeciderShould
{
  private static GameState LobbyWith(params int[] playerIds) =>
    GameState.Fold(
      new object[] { new V1.GameStarted(1) }
        .Concat(playerIds.Select(id => new V2.PlayerJoined(id, $"P{id}", PlayerColors.For(id))))
        .ToArray());

  [Fact]
  public void EmitGamePlayStartedWhenTheHostBeginsWithEnoughPlayers()
  {
    var events = BeginGameDecider.Decide(new BeginGameCommand(1, 1), LobbyWith(1));

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.GamePlayStarted(1));
  }

  [Fact]
  public void RejectAPlayerWhoIsNotTheHost()
  {
    var events = BeginGameDecider.Decide(new BeginGameCommand(1, 2), LobbyWith(1, 2));

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.OnlyHostCanStartGame(2, 1));
  }

  [Fact]
  public void RejectStartingWithNoPlayers()
  {
    var events = BeginGameDecider.Decide(new BeginGameCommand(1, 1), LobbyWith());

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.NotEnoughPlayers(0, HasMinimumPlayers.Minimum));
  }

  [Fact]
  public void RejectBeginningAGameAlreadyInPlay()
  {
    var inPlay = GameState.Fold(
      new V1.GameStarted(1),
      new V2.PlayerJoined(1, "Alice", PlayerColors.For(1)),
      new V1.GamePlayStarted(1));

    var events = BeginGameDecider.Decide(new BeginGameCommand(1, 1), inPlay);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new V1.GameAlreadyInPlay(Farkle.SharedKernel.Turns.GameStage.Rolling));
  }
}
