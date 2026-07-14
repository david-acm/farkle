using Farkle.Domain.GameAggregate;
using Farkle.Features.KeepDice;
using Farkle.Features.RollDice;
using Farkle.Tests.Framework;
using FluentAssertions;
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
      (Action<Game>)(g => g.KeepDice(new KeepDiceCommand(1, 2, new[] { DieValue.Five, DieValue.One })))
    };
    yield return new[]
    {
      (Action<Game>)(g => g.KeepDiceV2(new KeepDiceCommand(1, 2, new[] { DieValue.Five, DieValue.One })))
    };
  }

  [Theory]
  [MemberData(nameof(KeepCommands))]
  internal void OnlyAllowToKeepByThePlayerInTurn(Action<Game> keepAction)
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    keepAction(Game);

    // Assert
    Changes.Should().ContainSingleEvent<PlayedOutOfTurn>();
  }

  [Fact]
  public void OnlyAllowToKeepFivesAndOnes_WhenThePlayerDidntGetAnyOtherTricks()
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    
    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.Four }));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.Four, DieValue.Four, DieValue.Four }));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    //Act
    Game.KeepDice(new KeepDiceCommand(1, 1,
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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    
    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1,
      new[] { DieValue.One, DieValue.Two, DieValue.Three, DieValue.Four, DieValue.Five, DieValue.Six }));

    // Assert
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void AllowAndScoreThreePairs()
  {
    // Arrange — a full six-dice three-pairs hand (no 1s/5s, no triplet).
    SetupDiceToRoll(new List<int> { 2, 2, 4, 4, 6, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1,
      new[] { DieValue.Two, DieValue.Two, DieValue.Four, DieValue.Four, DieValue.Six, DieValue.Six }));

    // Assert — keepable (the new keep-gate clause) and worth 1500.
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
    State.TurnScore.Should().Be(new Score(1500));
  }

  [Fact]
  public void AllowAndScoreTwoTriplets()
  {
    // Arrange — two three-of-a-kinds in one roll.
    SetupDiceToRoll(new List<int> { 2, 2, 2, 5, 5, 5 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1,
      new[] { DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Five, DieValue.Five, DieValue.Five }));

    // Assert — two triplets scores 2500 (beats the per-triplet values).
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();
    State.TurnScore.Should().Be(new Score(2500));
  }

  [Fact]
  public void AllowToKeepOnlyDiceThatWereRolled()
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    var diceValues = new[]
    {
      DieValue.One, DieValue.Two, DieValue.Three, DieValue.Four, DieValue.Five, DieValue.Six
    };

    var last           = State.TableCenter!;
    var diceToKeep     = diceValues.Where(d => !last.Contains(d));
    
    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1, diceToKeep));

    // Assert
    Changes.Should().ContainSingleEvent<DiceNotAllowedToBeKept>();
  }

  [Fact]
  public void RejectKeepingMoreCopiesOfADieThanAreOnTheTable()
  {
    // Arrange — a roll with exactly one 1 on the table.
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 6, 2 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act — try to keep two 1s when only one is on the table.
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One, DieValue.One }));

    // Assert — the second 1 is unavailable, so the keep is rejected. This guards the
    // count (multiset) semantics of PlayerHasThoseDice, not just value presence.
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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    var diceValues = new[] { DieValue.One };

    var tableCenter = State.TableCenter!;

    var diceToKeep = diceValues.First(d => tableCenter.Contains(d) && d == DieValue.One);

    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { diceToKeep }));

    diceToKeep = State.DiceKept.First();

    //Act
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { diceToKeep }));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, diceToKeep));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    
    // Act
    Game.KeepDice(new KeepDiceCommand(1, 1, diceToKeep));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One }));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One }));

    SetupDiceToRoll(new List<int>
    {
      1,
      1,
      3,
      3,
      4
    });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One }));

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
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One, DieValue.One, DieValue.One }));

    SetupDiceToRoll(new List<int> { 4, 4, 4 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.Four, DieValue.Four, DieValue.Four }));

    // Assert
    State.TableCenter.Should().HaveCount(6);
  }

  [Fact]
  public void DoubleTurnScoreOnConsecutiveStraights()
  {
    // Arrange: First roll gives a straight and a 1 and 5
    SetupDiceToRoll(new List<int>
    {
      4, 4, 4, 4, 1, 5
    });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    
    // Act: Keep the straight
    Game.KeepDice(new KeepDiceCommand(1, 1,
        new[] { DieValue.Four, DieValue.Four, DieValue.Four, DieValue.Four }));

    // Keep the 1 and 5 to clear the table center (so we get 6 new dice)
    Game.KeepDice(new KeepDiceCommand(1, 1,
        new[] { DieValue.One, DieValue.Five }));

    // Score should be 1000 + 150 = 1150
    State.TurnScore.Should().Be(new Score(1150));

    // Second roll gives another straight (and maybe 2 other dice)
    SetupDiceToRoll(new List<int>
    {
      3, 3, 3, 3, 2, 2
    });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    Changes.Should().NotContainAnyEvent<IErrorEvent>();
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();

    // Keep the second straight
    Game.KeepDice(new KeepDiceCommand(1, 1,
        new[] { DieValue.Three, DieValue.Three, DieValue.Three, DieValue.Three }));

    Changes.Should().NotContainAnyEvent<IErrorEvent>();
    Changes.Should().NotContainAnyEvent<DiceNotAllowedToBeKept>();

    // Assert: The score before this keep was 1150.
    // The straight gives 1000. Total = 2150.
    // Because it's consecutive straight, it doubles: 2150 * 2 = 4300.
    State.TurnScore.Should().Be(new Score(4300));
  }

  [Fact]
  public void NotAllowNonExistentPlayerToKeep()
  {
    // Arrange — the player in turn rolls
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act — player 99 is not part of the game
    Game.KeepDice(new KeepDiceCommand(1, 99, new[] { DieValue.Five, DieValue.One }));

    // Assert — the in-turn check rejects any id that isn't the current player
    var playedOutOfTurn = Changes.Should().ContainSingleEvent<PlayedOutOfTurn>();
    playedOutOfTurn.Should().Be(new PlayedOutOfTurn(99, 1));
  }

  [Fact]
  public void ResetTurnScoreToZeroOnFarkle()
  {
    // Arrange — score a single 1 (100 pts) on the first roll
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One }));
    State.TurnScore.Should().Be(new Score(100));

    // Act — re-roll the remaining five dice into a Farkle: no 1s, no 5s and
    // no three-of-a-kind, so nothing can be kept
    SetupDiceToRoll(new List<int> { 2, 3, 4, 6, 2 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Assert — busting wipes the accumulated turn score
    State.TurnScore.Should().Be(new Score(0));
  }

  public static IEnumerable<object[]> TricksAndScore()
  {
    yield return new object[] { "1 should add 100", new[] { 1, 2, 2, 3, 4, 4 }, new[] { DieValue.One }, 100 };
    yield return new object[]
    {
      // Three 1s are special-cased to 1000 (not face*100 = 100) — standard Farkle. (#177)
      "three 1s should add 1000", new[] { 1, 1, 1, 2, 3, 4 },
      new[] { DieValue.One, DieValue.One, DieValue.One }, 1000
    };
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
    // #35 — five and six of a kind, end-to-end through KeepDice (flat 2000 / 3000).
    yield return new object[]
    {
      "five 2s should add 2000", new[] { 2, 2, 2, 2, 2, 3 },
      new[] { DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Two }, 2000
    };
    yield return new object[]
    {
      "six 2s should add 3000", new[] { 2, 2, 2, 2, 2, 2 },
      new[] { DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Two }, 3000
    };
    // #270 — keeping multiple tricks in one keep scores the sum.
    yield return new object[]
    {
      "three 2s + two 5s should add 300", new[] { 2, 2, 2, 5, 5, 4 },
      new[] { DieValue.Two, DieValue.Two, DieValue.Two, DieValue.Five, DieValue.Five }, 300
    };
    yield return new object[]
    {
      "three 3s + one 1 should add 400", new[] { 3, 3, 3, 1, 4, 6 },
      new[] { DieValue.Three, DieValue.Three, DieValue.Three, DieValue.One }, 400
    };
  }
}
