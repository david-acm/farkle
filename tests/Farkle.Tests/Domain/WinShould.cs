using Farkle.Domain.GameAggregate;
using Farkle.Features.KeepDice;
using Farkle.Features.PassTurn;
using Farkle.Features.RollDice;
using Farkle.SharedKernel.Turns;
using Farkle.Tests.Framework;
using FluentAssertions;
using System.Collections.Immutable;
using Xunit.Abstractions;
using static Farkle.Domain.GameAggregate.DieValue;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

namespace Farkle.Tests.Domain;

public class WinShould : GameWithThreePlayersTest
{
  public WinShould(ITestOutputHelper output) : base(output)
  {
  }

  [Fact]
  public void EmitGameWonWhenPlayerReaches5000Points()
  {
    // Arrange — inject past-turn events to bring player 1's committed score to 4,700
    // and rotate the turn order back to player 1
    var p1 = State.Players[0];
    var p2 = State.Players[1];
    var p3 = State.Players[2];

    var events = Game.Current.ToList();
    events.Add(new TurnPassed(p1.Id, ImmutableArray.Create(p2, p3, p1), 4_700));
    events.Add(new TurnPassed(p2.Id, ImmutableArray.Create(p3, p1, p2), 0));
    events.Add(new TurnPassed(p3.Id, ImmutableArray.Create(p1, p2, p3), 0));
    Game.Load(events);

    // Roll three 5s (trips = 500 pts) to push total over 5,000
    SetupDiceToRoll(new List<int> { 5, 5, 5, 2, 3, 4 });
    Game.RollDiceV2(new RollDiceCommand(1, p1.Id));
    Game.KeepDice(new KeepDiceCommand(1, p1.Id, new[] { Five, Five, Five }));
    Game.PassTurn(new PassTurnCommand(1, p1.Id));

    // Assert
    var won = Changes.Should().ContainSingleEvent<GameWon>();
    won!.PlayerId.Should().Be(p1.Id);
    State.GameStage.Should().Be(GameStage.Finished);
    State.Winner.Should().NotBeNull();
    State.Winner!.Id.Should().Be(p1.Id);
  }

  [Fact]
  public void NotEmitGameWonWhenScoreIsBelow5000()
  {
    // Arrange — a normal turn with a low score
    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, 1));
    Game.KeepDice(new KeepDiceCommand(1, 1, new[] { One }));

    // Act
    Game.PassTurn(new PassTurnCommand(1, 1));

    // Assert
    Changes.Should().NotContain(e => e is GameWon);
    State.GameStage.Should().Be(GameStage.Rolling);
    State.Winner.Should().BeNull();
  }

  [Fact]
  public void EmitGameWonWithScoreOfExactly5000WhenPlayerReachesThreshold()
  {
    // Arrange — bring player 1's committed score to exactly 4,900 and rotate
    // the turn order back to player 1
    var p1 = State.Players[0];
    var p2 = State.Players[1];
    var p3 = State.Players[2];

    var events = Game.Current.ToList();
    events.Add(new TurnPassed(p1.Id, ImmutableArray.Create(p2, p3, p1), 4_900));
    events.Add(new TurnPassed(p2.Id, ImmutableArray.Create(p3, p1, p2), 0));
    events.Add(new TurnPassed(p3.Id, ImmutableArray.Create(p1, p2, p3), 0));
    Game.Load(events);

    // Roll two 5s (100 pts) so the total lands on exactly 5,000
    SetupDiceToRoll(new List<int> { 5, 5, 2, 3, 4, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, p1.Id));
    Game.KeepDice(new KeepDiceCommand(1, p1.Id, new[] { Five, Five }));
    Game.PassTurn(new PassTurnCommand(1, p1.Id));

    // Assert — the >= 5,000 threshold fires at the exact-equality boundary
    var won = Changes.Should().ContainSingleEvent<GameWon>();
    won!.PlayerId.Should().Be(p1.Id);
    won.Score.Should().Be(5_000);
    State.GameStage.Should().Be(GameStage.Finished);
    State.Winner!.Id.Should().Be(p1.Id);
  }

  [Fact]
  public void SetGameStageToFinishedOnWin()
  {
    var p1     = State.Players[0];
    var p2     = State.Players[1];
    var p3     = State.Players[2];
    var events = Game.Current.ToList();
    events.Add(new TurnPassed(p1.Id, ImmutableArray.Create(p2, p3, p1), 4_900));
    events.Add(new TurnPassed(p2.Id, ImmutableArray.Create(p3, p1, p2), 0));
    events.Add(new TurnPassed(p3.Id, ImmutableArray.Create(p1, p2, p3), 0));
    Game.Load(events);

    SetupDiceToRoll(new List<int> { 1, 2, 3, 4, 5, 6 });
    Game.RollDiceV2(new RollDiceCommand(1, p1.Id));
    Game.KeepDice(new KeepDiceCommand(1, p1.Id, new[] { One }));
    Game.PassTurn(new PassTurnCommand(1, p1.Id));

    State.GameStage.Should().Be(GameStage.Finished);
  }
}
