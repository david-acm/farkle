using HotDice.Domain.GameAggregate;
using HotDice.Features.BeginGame;
using HotDice.Features.JoinPlayer;
using HotDice.Features.StartGame;
using Moq;
using Xunit.Abstractions;

namespace HotDice.Tests;

public class GameWithThreePlayersTest
{
  private readonly   IRandom              _randomProvider;
  internal readonly Game                 Game;
  protected readonly ITestOutputHelper    Output;
  internal           IRandom              RandomProvider => _randomProvider;
  private            List<int>.Enumerator _enumerator;

  protected GameWithThreePlayersTest(ITestOutputHelper output)
  {
    Output = output;
    // Arrange
    _randomProvider = Mock.Of<IRandom>();
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
    var game = new Game(_randomProvider);

    // Act
    game.Start(new StartGameCommand(1));
    game.JoinPlayer(new JoinPlayerCommand(1, "David"));
    game.JoinPlayer(new JoinPlayerCommand(1, "Cristian"));
    game.JoinPlayer(new JoinPlayerCommand(1, "German"));
    // The host (player 1) begins play, moving the game out of the lobby into Rolling.
    game.BeginGame(new BeginGameCommand(1, 1));

    Game = new Game(_randomProvider);
    Game.Load(game.Changes.ToList());
  }

  internal GameState                   State   => Game.State;
  protected IReadOnlyCollection<object> Changes => Game.Changes;
  protected IReadOnlyCollection<object> Current => Game.Current.ToList().AsReadOnly();

  protected void SetupDiceToRoll(IEnumerable<int> values)
  {
    _enumerator = values.ToList().GetEnumerator();
    Mock.Get(_randomProvider)
      .Setup(s => s.Next(It.IsAny<int>(), It.IsAny<int>()))
      .Returns(() =>
      {
        _enumerator.MoveNext();
        return _enumerator.Current;
      });
  }
}
