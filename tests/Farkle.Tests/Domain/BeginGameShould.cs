using Farkle.Domain.GameAggregate;
using Farkle.Tests.Framework;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.Command;
using static Farkle.Domain.GameAggregate.GameEvents.V1;
using static Farkle.Domain.GameAggregate.GameStage;

namespace Farkle.Tests.Domain;

public class BeginGameShould
{
  private static Game LobbyWith(int players)
  {
    var game = new Game();
    game.Start(new StartGame(1));
    for (var i = 1; i <= players; i++)
      game.JoinPlayer(new JoinPlayer(1, $"Player{i}"));
    return game;
  }

  [Fact]
  public void StartGameInLobbyWhenCreated()
  {
    var game = LobbyWith(2);

    game.State.GameStage.Should().Be(WaitingForPlayers);
  }

  [Fact]
  public void TransitionToRollingWhenHostBeginsWithEnoughPlayers()
  {
    var game = LobbyWith(2);

    game.BeginGame(new BeginGame(1, 1));

    game.State.GameStage.Should().Be(Rolling);
    game.Changes.Should().ContainSingleEvent<GamePlayStarted>();
  }

  [Fact]
  public void RejectBeginFromNonHost()
  {
    var game = LobbyWith(2);

    game.BeginGame(new BeginGame(1, 2));

    game.State.GameStage.Should().Be(WaitingForPlayers);
    var error = game.Changes.Should().ContainSingleEvent<OnlyHostCanStartGame>();
    error.Should().Be(new OnlyHostCanStartGame(2, 1));
  }

  [Fact]
  public void RejectBeginWithFewerThanTwoPlayers()
  {
    var game = LobbyWith(1);

    game.BeginGame(new BeginGame(1, 1));

    game.State.GameStage.Should().Be(WaitingForPlayers);
    var error = game.Changes.Should().ContainSingleEvent<NotEnoughPlayers>();
    error!.PlayerCount.Should().Be(1);
  }

  [Fact]
  public void RejectBeginWhenGameAlreadyInPlay()
  {
    var game = LobbyWith(2);
    game.BeginGame(new BeginGame(1, 1));

    game.BeginGame(new BeginGame(1, 1));

    game.Changes.Should().ContainSingleEvent<GameAlreadyInPlay>();
    game.Changes.Should().ContainSingleEvent<GamePlayStarted>();
  }

  [Fact]
  public void RejectRollBeforePlayHasBegun()
  {
    var game = LobbyWith(2);

    game.RollDiceV2(new RollDice(1, 1));

    game.State.GameStage.Should().Be(WaitingForPlayers);
    game.State.TableCenter.Should().BeEmpty();
    game.Changes.Should().ContainSingleEvent<RolledTwice>();
  }

  [Fact]
  public void RejectJoinAfterPlayHasBegun()
  {
    var game = LobbyWith(2);
    game.BeginGame(new BeginGame(1, 1));

    game.JoinPlayer(new JoinPlayer(1, "Latecomer"));

    game.State.Players.Should().HaveCount(2);
  }
}
