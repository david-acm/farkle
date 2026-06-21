using Farkle.Domain.GameAggregate;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.Command;

namespace Farkle.Tests.Domain;

// #244 — TurnNumber is the server-assigned, replay-derived turn ordinal used as a telemetry
// entity key. 0 in the lobby, 1 once play begins, +1 on every pass.
public class TurnNumberShould
{
  private static Game StartedGameWith(int players)
  {
    var game = new Game();
    game.Start(new StartGame(1));
    for (var i = 1; i <= players; i++)
      game.JoinPlayer(new JoinPlayer(1, $"Player{i}"));
    game.BeginGame(new BeginGame(1, 1));
    return game;
  }

  [Fact]
  public void BeZeroInTheLobbyBeforePlayBegins()
  {
    var game = new Game();
    game.Start(new StartGame(1));
    game.JoinPlayer(new JoinPlayer(1, "Alice"));

    game.State.TurnNumber.Should().Be(0);
  }

  [Fact]
  public void BeOneWhenPlayBegins()
  {
    var game = StartedGameWith(2);

    game.State.TurnNumber.Should().Be(1);
  }

  [Fact]
  public void IncrementOnEachPass()
  {
    var game = StartedGameWith(2);

    game.RollDiceV2(new RollDice(1, 1));
    game.PassTurn(new PassTurn(1, 1)); // player 1 → turn 2
    game.State.TurnNumber.Should().Be(2);

    game.RollDiceV2(new RollDice(1, 2));
    game.PassTurn(new PassTurn(1, 2)); // player 2 → turn 3
    game.State.TurnNumber.Should().Be(3);
  }
}
