using HotDice.Domain.GameAggregate;
using HotDice.Features.BeginGame;
using HotDice.Features.JoinPlayer;
using HotDice.Features.RollDice;
using HotDice.Features.StartGame;
using HotDice.Tests.Framework;
using FluentAssertions;
using static HotDice.Domain.GameAggregate.GameEvents.V1;
using static HotDice.SharedKernel.Turns.GameStage;

namespace HotDice.Tests.Domain;

public class BeginGameShould
{
  private static Game LobbyWith(int players)
  {
    var game = new Game();
    game.Start(new StartGameCommand(1));
    for (var i = 1; i <= players; i++)
      game.JoinPlayer(new JoinPlayerCommand(1, $"Player{i}"));
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

    game.BeginGame(new BeginGameCommand(1, 1));

    game.State.GameStage.Should().Be(Rolling);
    game.Changes.Should().ContainSingleEvent<GamePlayStarted>();
  }

  [Fact]
  public void RejectBeginFromNonHost()
  {
    var game = LobbyWith(2);

    game.BeginGame(new BeginGameCommand(1, 2));

    game.State.GameStage.Should().Be(WaitingForPlayers);
    var error = game.Changes.Should().ContainSingleEvent<OnlyHostCanStartGame>();
    error.Should().Be(new OnlyHostCanStartGame(2, 1));
  }

  [Fact]
  public void AllowHostToBeginWithASinglePlayer()
  {
    var game = LobbyWith(1);

    game.BeginGame(new BeginGameCommand(1, 1));

    game.State.GameStage.Should().Be(Rolling);
    game.Changes.Should().ContainSingleEvent<GamePlayStarted>();
  }

  [Fact]
  public void RejectBeginWithNoPlayers()
  {
    var game = LobbyWith(0);

    game.BeginGame(new BeginGameCommand(1, 1));

    game.State.GameStage.Should().Be(WaitingForPlayers);
    var error = game.Changes.Should().ContainSingleEvent<NotEnoughPlayers>();
    error!.PlayerCount.Should().Be(0);
    error.Minimum.Should().Be(1);
  }

  [Fact]
  public void RejectBeginWhenGameAlreadyInPlay()
  {
    var game = LobbyWith(2);
    game.BeginGame(new BeginGameCommand(1, 1));

    game.BeginGame(new BeginGameCommand(1, 1));

    game.Changes.Should().ContainSingleEvent<GameAlreadyInPlay>();
    game.Changes.Should().ContainSingleEvent<GamePlayStarted>();
  }

  [Fact]
  public void RejectRollBeforePlayHasBegun()
  {
    var game = LobbyWith(2);

    game.RollDiceV2(new RollDiceCommand(1, 1));

    game.State.GameStage.Should().Be(WaitingForPlayers);
    game.State.TableCenter.Should().BeEmpty();
    game.Changes.Should().ContainSingleEvent<RolledTwice>();
  }

  [Fact]
  public void RejectJoinAfterPlayHasBegun()
  {
    var game = LobbyWith(2);
    game.BeginGame(new BeginGameCommand(1, 1));

    game.JoinPlayer(new JoinPlayerCommand(1, "Latecomer"));

    game.State.Players.Should().HaveCount(2);
  }
}
