namespace WebApp.ReadModel;

// #156 — the denormalized per-game read model. One row per game holding the serialized
// GameState snapshot the projector maintains; GET reads StateJson instead of replaying.
public class GameView
{
    // The game's own id (not database-generated) — also the projection key.
    public int GameId { get; set; }

    // Serialized GameState (Farkle.Application.GameStateSerializer).
    public string StateJson { get; set; } = string.Empty;

    // The $all global position of the last event folded into this row (observability /
    // idempotency).
    public long Position { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
