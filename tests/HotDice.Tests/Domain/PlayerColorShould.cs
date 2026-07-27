using HotDice.Domain.GameAggregate;
using HotDice.Features.JoinPlayer;
using HotDice.Features.StartGame;
using FluentAssertions;
using V2 = HotDice.Domain.GameAggregate.GameEvents.V2;

namespace HotDice.Tests.Domain;

public class PlayerColorShould
{
  [Fact]
  public void AssignDistinctPaletteColorsInJoinOrder()
  {
    // Arrange
    var game = new Game();
    game.Start(new StartGameCommand(1));

    // Act
    game.JoinPlayer(new JoinPlayerCommand(1, "David"));
    game.JoinPlayer(new JoinPlayerCommand(1, "Cristian"));
    game.JoinPlayer(new JoinPlayerCommand(1, "German"));

    // Assert — every player carries a non-empty colour, all distinct, assigned by join order.
    var colors = game.State.Players.Select(p => p.Color).ToList();
    colors.Should().OnlyHaveUniqueItems();
    colors.Should().AllSatisfy(c => c.Should().NotBeNullOrWhiteSpace());
    game.State.GetPlayer(1).Color.Should().Be(PlayerColors.For(1));
    game.State.GetPlayer(2).Color.Should().Be(PlayerColors.For(2));
    game.State.GetPlayer(3).Color.Should().Be(PlayerColors.For(3));
  }

  [Fact]
  public void EmitPlayerJoinedV2CarryingTheColor()
  {
    // Arrange
    var game = new Game();
    game.Start(new StartGameCommand(1));

    // Act
    game.JoinPlayer(new JoinPlayerCommand(1, "David"));

    // Assert — the new V2 event carries the assigned colour (V1 is left untouched).
    var joined = game.Changes.OfType<V2.PlayerJoined>().Single();
    joined.Id.Should().Be(1);
    joined.Name.Should().Be("David");
    joined.Color.Should().Be(PlayerColors.For(1));
  }

  [Fact]
  public void AssignTheExpectedIdentityColoursByJoinOrder()
  {
    // #248 — player identity colours: P1 yellow, P2 light blue, P3 green, P4 pink.
    PlayerColors.For(1).Should().Be("#FFE600", "player 1 is yellow");
    PlayerColors.For(2).Should().Be("#40C4FF", "player 2 is light blue");
    PlayerColors.For(3).Should().Be("#69F0AE", "player 3 is green");
    PlayerColors.For(4).Should().Be("#FF2D6B", "player 4 is pink");
  }

  [Fact]
  public void WrapThePaletteWhenMorePlayersThanColors()
  {
    // The palette wraps so a game with more players than colours still assigns a colour.
    PlayerColors.For(1).Should().Be(PlayerColors.For(1 + PlayerColors.Palette.Length));
    PlayerColors.For(PlayerColors.Palette.Length + 2).Should().Be(PlayerColors.For(2));
  }
}
