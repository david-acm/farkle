using Eventuous.Subscriptions.Checkpoints;

namespace WebApp.ReadModel;

// #156 — durable checkpoint store for the GameView projector. Eventuous 0.15-beta ships no
// first-party EF/Postgres checkpoint store, so this persists the last committed $all position
// in the read-model database. Opens a DI scope per call (the subscription is a singleton).
public sealed class PostgresCheckpointStore(IServiceScopeFactory scopeFactory) : ICheckpointStore
{
    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();
        var row = await db.Checkpoints.FindAsync([checkpointId], cancellationToken);
        return new Checkpoint(checkpointId, row?.Position is { } p ? (ulong)p : null);
    }

    public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        var row = await db.Checkpoints.FindAsync([checkpoint.Id], cancellationToken);
        if (row is null)
        {
            row = new SubscriptionCheckpoint { Id = checkpoint.Id };
            db.Checkpoints.Add(row);
        }

        row.Position = checkpoint.Position is { } p ? (long)p : null;

        await db.SaveChangesAsync(cancellationToken);
        return checkpoint;
    }
}
