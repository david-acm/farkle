using Farkle.Domain.GameAggregate;
using FluentAssertions;
using Farkle.Tests.Framework;
using Xunit.Abstractions;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

namespace Farkle.Tests.Domain;

public class KeepDiceShould : GameWithThreePlayersTest
{
  public KeepDiceShould(ITestOutputHelper helper)
    : base(helper)
  {
  }

  public static IEnumerable<object[]> KeepCommands()
  {
    yield return new[]
    {
      (Action<Game>)(g => g.KeepDice(new Command.KeepDice(1, 2, new[] { DieValue.Five, DieValue.One })))
    };
    yield return new[]
    {
      (Action<Game>)(g => g.KeepDiceV2(new Command.KeepDice(1, 2, new[] { DieValue.Five, DieValue.One })))
    };
  }

  [Theory]
  [MemberData(nameof(KeepCommands))]
  internal void OnlyAllowToKeepByThePlayerInTurn(Action<Game> keepAction)
  {
    // Arrange
    Game.RollDiceV2(new Command.RollDice(1, 1));

    // Act
    keepAction(Game);

    // Assert
    Changes.Should().ContainSingleEvent<PlayedOutOfTurn>();
  }

  [Fact]
  public void OnlyAllowToKeepFivesAndOnes_WhenThePlayerDidntGetAnyOtherTricks()
  {
    // Arrange
    Game.RollDiceV2(new Command.RollDice(1, 1));
    
    // Act
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.Four }));

    // Assert
    Changes.Should().ContainSingleEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void AllowToKeepTrips()
  {
    // Arrange
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
    Game.RollDiceV2(new Command.RollDice(1, 1));

    // Act
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.Four, DieValue.Four, DieValue.Four }));

    // Assert
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void AllowToKeepAStraight()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      4,
      4,
      4,
      4,
      1,
      2,
      3
    });
    Game.RollDiceV2(new Command.RollDice(1, 1));

    //Act
    Game.KeepDice(new Command.KeepDice(1, 1,
        new[] { DieValue.Four, DieValue.Four, DieValue.Four, DieValue.Four }));

    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void AllowToKeepStair()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      1,
      2,
      3,
      4,
      5,
      6
    });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    
    // Act
    Game.KeepDice(new Command.KeepDice(1, 1,
      new[] { DieValue.One, DieValue.Two, DieValue.Three, DieValue.Four, DieValue.Five, DieValue.Six }));


    // Assert
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void AllowToKeepOnlyDiceThatWereRolled()
  {
    // Arrange
    Game.RollDiceV2(new Command.RollDice(1, 1));
    var diceValues = new[]
    {
      DieValue.One, DieValue.Two, DieValue.Three, DieValue.Four, DieValue.Five, DieValue.Six
    };

    var last           = State.TableCenter!;
    var diceToKeep     = diceValues.Where(d => !last.Contains(d));
    
    // Act
    Game.KeepDice(new Command.KeepDice(1, 1, diceToKeep));

    // Assert
    Changes.Should().ContainSingleEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void AllowToKeepOnlyDiceThatAreStillInTheTable()
  {
    // Arrange
    var values = new List<int>
    {
      1,
      1,
      3,
      4,
      5,
      6
    };
    SetupDiceToRoll(values);
    Game.RollDiceV2(new Command.RollDice(1, 1));
    var diceValues = new[] { DieValue.One };

    var tableCenter = State.TableCenter!;

    var diceToKeep = diceValues.First(d => tableCenter.Contains(d) && d == DieValue.One);

    Game.KeepDice(new Command.KeepDice(1, 1, new[] { diceToKeep }));

    diceToKeep = State.DiceKept.First();

    //Act
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { diceToKeep }));

    // Assert
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void RemoveDiceFromTableCenter()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      1,
      2,
      3,
      4,
      5,
      6
    });
    var diceToKeep = new[] { DieValue.One, DieValue.Five };

    // Act
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, diceToKeep));

    // Assert
    State.TableCenter.Should().HaveCount(4);
  }

  [Theory]
  [MemberData(nameof(TricksAndScore))]
  internal void AddTurnScoreToPlayer(
    string      reason,
    int[]       rolledDice,
    DieValue[] diceToKeep,
    int         expectedScore)
  {
    // Arrange
    SetupDiceToRoll(rolledDice);
    Game.RollDiceV2(new Command.RollDice(1, 1));
    
    // Act
    Game.KeepDice(new Command.KeepDice(1, 1, diceToKeep));

    // Assert
    State.TurnScore.Should()
      .Be(new Score(expectedScore),
        $"{reason} but got {State.TurnScore}");
  }

  [Fact]
  public void ResetScoreIfPlayerGetsNoTricks()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      1,
      2,
      3,
      4,
      5,
      6
    });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.One }));

    SetupDiceToRoll(new List<int>
    {
      2,
      2,
      3,
      3,
      4,
      6
    });
    // Act
    Game.RollDiceV2(new Command.RollDice(1, 1));

    // Assert
    State.TurnScore.Should().Be(new Score(0));
  }

  [Fact]
  public void AddToTurnScore()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      1,
      2,
      3,
      4,
      5,
      6
    });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.One }));

    SetupDiceToRoll(new List<int>
    {
      1,
      1,
      3,
      3,
      4
    });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.One }));

    // Assert
    State.TurnScore.Should().Be(new Score(200));
  }

  [Fact]
  public void ResetDiceInTableCenterWhenAllDiceHaveBeenKept()
  {
    // Arrange
    SetupDiceToRoll(new List<int>
    {
      1,
      1,
      1,
      2,
      3,
      4
    });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.One, DieValue.One, DieValue.One }));

    SetupDiceToRoll(new List<int> { 4, 4, 4 });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.Four, DieValue.Four, DieValue.Four }));

    // Assert
    State.TableCenter.Should().HaveCount(6);
  }

  public static IEnumerable<object[]> TricksAndScore()
  {
    yield return new object[] { "1 should add 100", new[] { 1, 2, 2, 3, 4, 4 }, new[] { DieValue.One }, 100 };
    yield return new object[]
    {
      "1 and 5 should add 150", new[] { 1, 1, 2, 3, 4, 5 }, new[] { DieValue.One, DieValue.Five, DieValue.One },
      250
    };
    yield return new object[]
    {
      "2, 2, 2 should add 200", new[] { 3, 3, 3, 3, 4, 4 },
      new[] { DieValue.Three, DieValue.Three, DieValue.Three }, 300
    };
    yield return new object[]
    {
      "4, 4, 4, 4 should add 1000", new[] { 3, 3, 4, 4, 4, 4 },
      new[] { DieValue.Four, DieValue.Four, DieValue.Four, DieValue.Four }, 1000
    };
  }
}
