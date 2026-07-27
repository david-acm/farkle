using System.Collections.Immutable;

namespace HotDice.Domain.GameAggregate;

// A fixed, ordered palette of player identity colours. Each player is assigned a colour
// deterministically by join order (player ids are handed out sequentially from 1), so every
// client — and every event replay — agrees on who owns which colour without persisting any
// extra state. The colours are chosen to be distinct from one another and legible on the dark
// theme background (#0A0A1A). The palette wraps when a game has more players than colours.
internal static class PlayerColors
{
  public static readonly ImmutableArray<string> Palette =
  [
    "#FFE600", // yellow      (player 1 — the original accent)
    "#40C4FF", // light blue  (player 2)
    "#69F0AE", // green       (player 3)
    "#FF2D6B", // pink        (player 4)
    "#B388FF", // lavender    (player 5)
    "#FF9100", // orange      (player 6)
  ];

  // Resolve the colour for a player id (1-based join order). Wraps around the palette so a
  // game with more players than colours still gets a valid, deterministic colour.
  public static string For(int playerId)
  {
    var index = ((playerId - 1) % Palette.Length + Palette.Length) % Palette.Length;
    return Palette[index];
  }
}
