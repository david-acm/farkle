using System.Collections.Immutable;
using Farkle.Domain.GameAggregate;
using Farkle.Features.StartGame;
using Farkle.SharedKernel.Turns;
using FluentAssertions;
using static Farkle.Domain.GameAggregate.GameEvents.V1;

namespace Farkle.Tests.Features.StartGame;

// The template for every slice's tests under the Critter Stack plan (#301): a pure decider
// test — arrange a state, call Decide, assert the emitted events. No aggregate, no mocks, no I/O.
public class StartGameDeciderShould
{
  [Fact]
  public void EmitGameStartedForANewGame()
  {
    var events = StartGameDecider.Decide(new Command.StartGame(42), new GameState());

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new GameStarted(42));
  }

  [Fact]
  public void RejectStartingAnAlreadyStartedGame()
  {
    var started = StartedGame(42);

    var events = StartGameDecider.Decide(new Command.StartGame(42), started);

    events.Should().ContainSingle()
      .Which.Should().BeEquivalentTo(new GameAlreadyStarted(42));
  }

  private static GameState StartedGame(int id) => GameState.FromSnapshot(
    id: id,
    gameStage: GameStage.WaitingForPlayers,
    winner: null,
    turnScore: 0,
    players: ImmutableArray<Player>.Empty,
    tableCenter: ImmutableArray<DieValue>.Empty,
    diceKept: ImmutableArray<DieValue>.Empty,
    diceSetAside: ImmutableArray<DieValue>.Empty,
    straightsKeptThisTurn: 0,
    scoreTable: ImmutableDictionary<int, int>.Empty);
}
