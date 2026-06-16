namespace Farkle.Infrastructure.ReadModel;

// #156 — persisted checkpoint for the GameView projector subscription, so it resumes from
// where it left off across restarts (unlike the broadcaster's NoOpCheckpointStore, which
// re-reads from the start — fine for broadcasts, wrong for a stateful projection).
public class SubscriptionCheckpoint
{
    // The Eventuous subscription id.
    public string Id { get; set; } = string.Empty;

    // The last committed $all position; null means "from the beginning".
    public long? Position { get; set; }
}
