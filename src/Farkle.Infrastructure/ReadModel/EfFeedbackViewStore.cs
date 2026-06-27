using Farkle.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.Infrastructure.ReadModel;

// #277 — Postgres-backed IFeedbackViewStore. The projector is resolved from a singleton scope (the
// subscription is a singleton hosted service), so each save opens its own DI scope to get a fresh,
// correctly-scoped ReadModelDbContext (mirrors EfGameViewStore). Insert is idempotent: keyed by the
// unique $all position, so an at-least-once redelivery is a no-op.
public sealed class EfFeedbackViewStore(IServiceScopeFactory scopeFactory) : IFeedbackViewStore
{
    public async Task SaveAsync(FeedbackViewRecord record, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReadModelDbContext>();

        if (await db.FeedbackViews.AnyAsync(x => x.Position == record.Position, ct))
            return; // already projected — idempotent

        db.FeedbackViews.Add(new FeedbackView
        {
            Position  = record.Position,
            SessionId = record.SessionId,
            GameId    = record.GameId,
            PlayerId  = record.PlayerId,
            Stage     = record.Stage,
            Message   = record.Message,
            Sentiment = record.Sentiment,
            Route     = record.Route,
            CreatedAt = record.CreatedAt,
        });

        await db.SaveChangesAsync(ct);
    }
}
