using Farkle.Application;

namespace WebApp.ReadModel;

// #156 — Postgres-backed IGameViewStore. The projector (and GET) are resolved from singleton
// scopes (the subscription is a singleton hosted service), so each operation opens its own
// DI scope to get a fresh, correctly-scoped ReadModelDbContext.
public sealed class EfGameViewStore(IServiceScopeFactory scopeFactory) : IGameViewStore
{
    public async Task<string?> GetAsync(int gameId, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();
        var row = await db.GameViews.FindAsync([gameId], ct);
        return row?.StateJson;
    }

    public async Task UpsertAsync(int gameId, string stateJson, ulong position, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        var row = await db.GameViews.FindAsync([gameId], ct);
        if (row is null)
        {
            row = new GameView { GameId = gameId };
            db.GameViews.Add(row);
        }

        row.StateJson = stateJson;
        row.Position  = (long)position;
        row.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}
