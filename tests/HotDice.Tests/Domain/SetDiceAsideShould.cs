using HotDice.Domain.GameAggregate;
using HotDice.Features.KeepDice;
using HotDice.Features.RollDice;
using HotDice.Features.SetDiceAside;
using HotDice.Tests.Framework;
using FluentAssertions;
using Xunit.Abstractions;
using static HotDice.Domain.GameAggregate.GameEvents.V1;

namespace HotDice.Tests.Domain;

// #159 — "set aside" is a transient selection of which rolled dice the player intends to
// keep. It is now a first-class command/event so it can be persisted and broadcast to
// spectators, but it does NOT score or permanently keep dice — that is still Keep's job.
public class SetDiceAsideShould : GameWithThreePlayersTest
{
  public SetDiceAsideShould(ITestOutputHelper helper) : base(helper)
  {
  }

  [Fact]
  public void RecordTheSelectionForThePlayerInTurn()
  {
    // Arrange — table center is [1,2,3,4,5,6]
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));

    // Assert
    Changes.Should().ContainSingleEvent<DiceSetAside>();
    State.DiceSetAside.Should().ContainSingle().Which.Should().Be(DieValue.One);
  }

  [Fact]
  public void NotRemoveSetAsideDiceFromTheTableCenter()
  {
    // Arrange
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act — setting a die aside is a non-destructive selection overlay.
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));

    // Assert — the table still holds all six dice; Keep (untouched) still validates
    // against the full table center.
    State.TableCenter.Should().HaveCount(6);
  }

  [Fact]
  public void AccumulateMultipleSelections()
  {
    // Arrange
    SetupDiceToRoll(new List<int> { 1, 1, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.Five));

    // Assert
    State.DiceSetAside.Should().BeEquivalentTo(new[] { DieValue.One, DieValue.Five });
  }

  [Fact]
  public void RejectSettingAsideByAPlayerOutOfTurn()
  {
    // Arrange
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act — player 2 isn't in turn
    Game.SetDiceAside(new SetDiceAsideCommand(1, 2, DieValue.One));

    // Assert
    Changes.Should().ContainSingleEvent<PlayedOutOfTurn>();
    State.DiceSetAside.Should().BeEmpty();
  }

  [Fact]
  public void RejectSettingAsideADieThatIsNotOnTheTable()
  {
    // Arrange — no 6 on the table
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 1 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.Six));

    // Assert
    Changes.Should().ContainSingleEvent<DieNotAvailableToSetAside>();
    State.DiceSetAside.Should().BeEmpty();
  }

  [Fact]
  public void RejectSettingAsideMoreCopiesOfADieThanAreOnTheTable()
  {
    // Arrange — exactly one 1 on the table
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));

    // Act — try to set aside a second 1
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));

    // Assert — multiset semantics: only one 1 exists, so the second is rejected.
    Changes.Should().ContainSingleEvent<DieNotAvailableToSetAside>();
    State.DiceSetAside.Should().ContainSingle();
  }

  [Fact]
  public void NotScoreOrKeepDice()
  {
    // Arrange
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));

    // Act — setting aside a scoring die does not add to the turn score nor keep it.
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));

    // Assert
    State.TurnScore.Should().Be(new Score(0));
    State.DiceKept.Should().BeEmpty();
  }

  [Fact]
  public void BeClearedOnceTheDiceAreKept()
  {
    // Arrange
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.SetDiceAside(new SetDiceAsideCommand(1, 1, DieValue.One));

    // Act — committing the selection via Keep ends the selection.
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { DieValue.One }));

    // Assert
    State.DiceSetAside.Should().BeEmpty();
  }
}
