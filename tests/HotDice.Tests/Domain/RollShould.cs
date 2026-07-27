using HotDice.Domain.GameAggregate;
using HotDice.Features.KeepDice;
using HotDice.Features.PassTurn;
using HotDice.Features.RollDice;
using HotDice.Tests.Framework;
using FluentAssertions;
using Moq;
using Xunit.Abstractions;
using static HotDice.Domain.GameAggregate.DieValue;
using static HotDice.Domain.GameAggregate.GameEvents;

namespace HotDice.Tests.Domain;

public class RollShould : GameWithThreePlayersTest
{
  public RollShould(ITestOutputHelper output) : base(output)
  {
  }

  [Fact]
  public void AllowPlayerToRoll()
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    
    // Act
    Game.PassTurn(new PassTurnCommand(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var diceRolled = Changes.Where(e => e is V2.DiceRolled).Should().HaveCount(1).And.Subject;
    diceRolled.Should()
      .ContainSingle(e =>
        ((V2.DiceRolled)e).PlayerId == 1);
  }

  [Fact]
  public void V1AllowPlayerToRoll()
  {
    // Arrange
    Game.RollDiceV1(new RollDiceCommand(1, 1));

    // Act
    Game.PassTurn(new PassTurnCommand(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var diceRolled = Changes.Where(e => e is V1.DiceRolled).Should().HaveCount(1).And.Subject;
    diceRolled.Should()
      .ContainSingle(e =>
        ((V1.DiceRolled)e).PlayerId == 1);
  }

  [Fact]
  public void NotAllowPlayerToRollOutOfTurn()
  {
    // Act
    Game.RollDiceV2(new RollDiceCommand(1, 2));

    // Assert
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<V1.PlayedOutOfTurn>();
    playedOutOfTurn.Should().Be(new V1.PlayedOutOfTurn(2, 1));
  }

  [Fact]
  public void NotAllowNonExistentPlayerToRoll()
  {
    // Act — player 99 is not part of the game
    Game.RollDiceV2(new RollDiceCommand(1, 99));

    // Assert — the in-turn check rejects any id that isn't the current player;
    // there is no separate "player not found" error
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<V1.PlayedOutOfTurn>();
    playedOutOfTurn.Should().Be(new V1.PlayedOutOfTurn(99, 1));
  }

  [Fact]
  public void NotAllowPlayerToRollTwiceBeforeKeepingSomeDice()
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      4,
      2,
      1,
      2,
      3
    });
    
    // Act
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<V1.RolledTwice>();
    playedOutOfTurn!.Player.Should().Be(1);
  }

  [Fact]
  public void NotAllowNextPlayerToPlayUntilPlayerPasses()
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      4,
      2,
      1,
      2,
      3
    });
    
    // Act
    Game.RollDiceV2(new RollDiceCommand(1, 2));

    // Assert
    var playedOutOfTurn = Changes.Where(e => e is V1.PlayedOutOfTurn).Should().ContainSingle().And.Subject;
    playedOutOfTurn.Should()
      .Satisfy(e =>
        ((V1.PlayedOutOfTurn)e).TriedToPlay    == 2 &&
        ((V1.PlayedOutOfTurn)e).ExpectedPlayer == 1);
  }

  [Fact]
  public void RequestDieValuesUpToAndIncludingSix()
  {
    // Act
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Assert — Random.Next upper bound must be 7 (exclusive) so that 6 is reachable
    Mock.Get(RandomProvider)
      .Verify(r => r.Next(1, 7), Times.Exactly(6));
  }

  [Fact]
  public void RollOnlyAvailableDiceAtTheTableCenter()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      5,
      2,
      1,
      2
    });
    // Act
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { One }));

    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      5,
      2,
      1
    });

    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Assert
    State.TableCenter!.Should().HaveCount(5);
  }
}
