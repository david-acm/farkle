using Microsoft.EntityFrameworkCore;

namespace Farkle.Infrastructure.ReadModel;

// #156 — the CQRS read-model store (Postgres). Shares the same database as Identity but uses
// its own migrations-history table (configured at registration) so the two DbContexts never
// collide on __EFMigrationsHistory.
public class ReadModelDbContext(DbContextOptions<ReadModelDbContext> options) : DbContext(options)
{
    public DbSet<GameView> GameViews => Set<GameView>();

    public DbSet<SubscriptionCheckpoint> Checkpoints => Set<SubscriptionCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameView>(e =>
        {
            e.HasKey(x => x.GameId);
            e.Property(x => x.GameId).ValueGeneratedNever(); // the game's own id
            e.Property(x => x.StateJson).IsRequired();
        });

        modelBuilder.Entity<SubscriptionCheckpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(200);
        });
    }
}
