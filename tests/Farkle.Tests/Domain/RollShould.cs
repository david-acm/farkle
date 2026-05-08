using Farkle.Domain.GameAggregate;
using FluentAssertions;
using Farkle.Tests.Framework;
using Moq;
using Xunit.Abstractions;
using static Farkle.Domain.GameAggregate.Command;
using static Farkle.Domain.GameAggregate.DieValue;
using static Farkle.Domain.GameAggregate.GameEvents;

namespace Farkle.Tests.Domain;

public class RollShould : GameWithThreePlayersTest
{
  public RollShould(ITestOutputHelper output) : base(output)
  {
  }

  [Fact]
  public void AllowPlayerToRoll()
  {
    // Arrange
    Game.RollDiceV2(new RollDice(1, 1));
    
    // Act
    Game.PassTurn(new PassTurn(1, 1));

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
    var rollEvent = new V1.DiceRolled(1, new[] { 1, 2, 3, 4, 5, 6 }, new Score(0));
    var events    = Game.Current.ToList();
    events.Add(rollEvent);
    Game.Load(events);
    
    // Act
    Game.PassTurn(new PassTurn(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var diceRolled = Current.Where(e => e is V1.DiceRolled).Should().HaveCount(1).And.Subject;
    diceRolled.Should()
      .ContainSingle(e =>
        ((V1.DiceRolled)e).PlayerId == 1);
  }

  [Fact]
  public void NotAllowPlayerToRollOutOfTurn()
  {
    // Act
    Game.RollDiceV2(new RollDice(1, 2));

    // Assert
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<V1.PlayedOutOfTurn>();
    playedOutOfTurn.Should().Be(new V1.PlayedOutOfTurn(2, 1));
  }

  [Fact]
  public void NotAllowPlayerToRollTwiceBeforeKeepingSomeDice()
  {
    // Arrange
    Game.RollDiceV2(new RollDice(1, 1));
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
    Game.RollDiceV2(new RollDice(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<V1.RolledTwice>();
    playedOutOfTurn!.Player.Should().Be(1);
  }

  [Fact]
  public void NotAllowNextPlayerToPlayUntilPlayerPasses()
  {
    // Arrange
    Game.RollDiceV2(new RollDice(1, 1));
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
    Game.RollDiceV2(new RollDice(1, 2));

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
    Game.RollDiceV2(new RollDice(1, 1));

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
    Game.RollDiceV2(new RollDice(1, 1));
    Game.KeepDice(new KeepDice(1, 1, new[] { One }));


    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      5,
      2,
      1
    });

    Game.RollDiceV2(new RollDice(1, 1));

    // Assert
    State.TableCenter!.Should().HaveCount(5);
  }
}
