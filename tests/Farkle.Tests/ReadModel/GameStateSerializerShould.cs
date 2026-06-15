using Farkle.Application;
using Farkle.Domain.GameAggregate;
using FluentAssertions;
using Xunit.Abstractions;

namespace Farkle.Tests.ReadModel;

// #156 — guards the read-model serializer and the incremental fold used by GameViewProjector.
public class GameStateSerializerShould : GameWithThreePlayersTest
{
  public GameStateSerializerShould(ITestOutputHelper helper) : base(helper)
  {
  }

  [Fact]
  public void RoundTripANonTrivialState()
  {
    // Arrange — drive the game into a rich mid-turn state (players, table, set-aside, scores).
    SetupDiceToRoll(new List<int> { 1, 1, 5, 2, 3, 4 });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.One));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.One }));

    var original = Game.State;

    // Act
    var roundTripped = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(original));

    // Assert — value equality on every persisted field (record equality can't be used
    // because State<T> carries per-instance event-handler delegates).
    AssertSameState(roundTripped, original);
  }

  [Fact]
  public void RoundTripAFreshState()
  {
    var fresh = new GameState();

    var roundTripped = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(fresh));

    AssertSameState(roundTripped, fresh);
  }

  [Fact]
  public void FoldIncrementallyToTheSameStateAsAReplay()
  {
    // Arrange — a full sequence of events (the base already started + joined + began).
    SetupDiceToRoll(new List<int> { 1, 1, 5, 2, 3, 4 });
    Game.RollDiceV2(new Command.RollDice(1, 1));
    Game.SetDiceAside(new Command.SetDiceAside(1, 1, DieValue.Five));
    Game.ReturnDice(new Command.ReturnDice(1, 1, DieValue.Five));
    Game.KeepDice(new Command.KeepDice(1, 1, new[] { DieValue.One, DieValue.One }));
    Game.PassTurn(new Command.PassTurn(1, 1));

    var replayed = Game.State;

    // Act — replicate the projector: fold one event at a time, serializing/deserializing
    // between each (exactly what GameViewProjector does across subscription deliveries).
    // Current is the full stream (the base Loads start/join/begin); Changes is only the
    // post-load events, so fold over Current to mirror a from-scratch projection.
    var folded = new GameState();
    foreach (var @event in Current)
    {
      folded = folded.When(@event);
      folded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(folded));
    }

    // Assert — the incrementally folded + persisted state matches a straight replay.
    AssertSameState(folded, replayed);
  }

  private static void AssertSameState(GameState actual, GameState expected)
  {
    actual.Id.Should().Be(expected.Id);
    actual.GameStage.Should().Be(expected.GameStage);
    actual.Winner.Should().Be(expected.Winner);
    actual.TurnScore.Should().Be(expected.TurnScore);
    actual.StraightsKeptThisTurn.Should().Be(expected.StraightsKeptThisTurn);
    actual.Players.Should().Equal(expected.Players);
    actual.TableCenter.Should().Equal(expected.TableCenter);
    actual.DiceKept.Should().Equal(expected.DiceKept);
    actual.DiceSetAside.Should().Equal(expected.DiceSetAside);
    actual.ScoreTable.Should().BeEquivalentTo(expected.ScoreTable);
  }
}
