using Farkle.Domain.GameAggregate;
using FluentAssertions;
using Farkle.Tests.Framework;
using Xunit.Abstractions;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

namespace Farkle.Tests.Domain;

// #159 — "put back" is the companion to "set aside": it un-selects a die the player had
// staged. Like set aside, it is a transient, persisted/broadcast selection change — it
// never permanently keeps or scores dice.
public class ReturnDiceShould : GameWithThreePlayersTest
{
  public ReturnDiceShould(ITestOutputHelper helper) : base(helper)
  {
  }

  [Fact]
  public void RemoveADieFromTheSelection()
  {
    // Arrange — set aside a 1 and a 5
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.One));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.Five));

    // Act — put the 1 back
    Game.ReturnDice(new Command.ReturnDice(1, 1, DieValue.One));

    // Assert
    Changes.Should().ContainSingleEvent<DiceReturned>();
    State.DiceSetAside.Should().ContainSingle().Which.Should().Be(DieValue.Five);
  }

  [Fact]
  public void RemoveOnlyOneCopyOfADie()
  {
    // Arrange — two 1s set aside
    SetupDiceToRoll(new List<int> { 1, 1, 3, 4, 5, 6 });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.One));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.One));

    // Act — put one 1 back
    Game.ReturnDice(new Command.ReturnDice(1, 1, DieValue.One));

    // Assert — multiset semantics: exactly one 1 remains set aside.
    State.DiceSetAside.Should().ContainSingle().Which.Should().Be(DieValue.One);
  }

  [Fact]
  public void RejectReturningADieThatWasNotSetAside()
  {
    // Arrange
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new Command.RollDice(1, 1));

    // Act — nothing has been set aside yet
    Game.ReturnDice(new Command.ReturnDice(1, 1, DieValue.One));

    // Assert
    Changes.Should().ContainSingleEvent<DieNotSetAside>();
    State.DiceSetAside.Should().BeEmpty();
  }

  [Fact]
  public void RejectReturningByAPlayerOutOfTurn()
  {
    // Arrange
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.One));

    // Act — player 2 isn't in turn
    Game.ReturnDice(new Command.ReturnDice(1, 2, DieValue.One));

    // Assert
    Changes.Should().ContainSingleEvent<PlayedOutOfTurn>();
    State.DiceSetAside.Should().ContainSingle();
  }
}
