using Farkle.GameAggregate;
using FluentAssertions;
using Farkle.Tests.Framework;
using Xunit.Abstractions;
using static Farkle.GameAggregate.Command;
using static Farkle.GameAggregate.DiceValue;
using static Farkle.GameAggregate.GameEvents;

namespace Farkle.Tests.Domain;

public class RollShould : GameWithThreePlayersTest
{
  public RollShould(ITestOutputHelper output) : base(output)
  {
  }

  [Fact]
  public void AllowPlayerToRoll()
  {
    // Act
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.PassTurn(new Command.PassTurn(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var diceRolled = Changes.Where(e => e is GameEvents.V2.DiceRolled).Should().HaveCount(1).And.Subject;
    diceRolled.Should()
      .ContainSingle(e =>
        ((GameEvents.V2.DiceRolled)e).PlayerId == 1);
  }

  [Fact]
  public void V1AllowPlayerToRoll()
  {
    // Act
    var rollEvent = new GameEvents.V1.DiceRolled(1, new[] { 1, 2, 3, 4, 5, 6 }, new Score(0));
    var events    = Game.Current.ToList();
    events.Add(rollEvent);
    Game.Load(events);
    Game.PassTurn(new Command.PassTurn(1, 1));

    // Assert
    State.TableCenter.Should().HaveCount(6);
    var diceRolled = Current.Where(e => e is GameEvents.V1.DiceRolled).Should().HaveCount(1).And.Subject;
    diceRolled.Should()
      .ContainSingle(e =>
        ((GameEvents.V1.DiceRolled)e).PlayerId == 1);
  }

  [Fact]
  public void NotAllowPlayerToRollOutOfTurn()
  {
    // Act
    var action = () => Game.RollDiceV2(new Command.RollDice(1, 2));

    // Assert
    action.Should().Throw<PreconditionsFailedException>();
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<GameEvents.V1.PlayedOutOfTurn>();
    playedOutOfTurn.Should().Be(new GameEvents.V1.PlayedOutOfTurn(2, 1));
  }

  [Fact]
  public void NotAllowPlayerToRollTwiceBeforeKeepingSomeDice()
  {
    // Act
    Game.RollDiceV2(new Command.RollDice(1, 1));
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
    var action = () => Game.RollDiceV2(new Command.RollDice(1, 1));

    // Assert
    action.Should().Throw<PreconditionsFailedException>();
    State.TableCenter.Should().HaveCount(6);
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<GameEvents.V1.RolledTwice>();
    playedOutOfTurn!.Player.Should().Be(1);
  }

  [Fact]
  public void NotAllowNextPlayerToPlayUntilPlayerPasses()
  {
    // Act
    Game.RollDiceV2(new Command.RollDice(1, 1));
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

    var action = () => Game.RollDiceV2(new Command.RollDice(1, 2));

    // Assert
    action.Should().Throw<PreconditionsFailedException>();
    var playedOutOfTurn = Changes.Where(e => e is GameEvents.V1.PlayedOutOfTurn).Should().ContainSingle().And.Subject;
    playedOutOfTurn.Should()
      .Satisfy(e =>
        ((GameEvents.V1.PlayedOutOfTurn)e).TriedToPlay    == 2 &&
        ((GameEvents.V1.PlayedOutOfTurn)e).ExpectedPlayer == 1);
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
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { One }));


    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      5,
      2,
      1
    });

    Game.RollDiceV2(new Command.RollDice(1, 1));

    // Assert
    State.TableCenter!.Should().HaveCount(5);
  }
}
