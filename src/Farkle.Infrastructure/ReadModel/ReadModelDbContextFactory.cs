using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Farkle.Infrastructure.ReadModel;

// #156 — design-time factory so `dotnet ef migrations add ... -c ReadModelDbContext` works
// without booting the full host (which would connect to ESDB / start subscriptions). The
// connection string here is only used to scaffold migrations, never to connect; runtime
// configuration comes from Program.cs (the Identity Postgres, with its own history table).
public sealed class ReadModelDbContextFactory : IDesignTimeDbContextFactory<ReadModelDbContext>
{
    public ReadModelDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ReadModelDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=farkle_identity;Username=postgres;Password=postgres",
                b => b.MigrationsHistoryTable(ReadModelMigrations.HistoryTable))
            .Options;
        return new ReadModelDbContext(options);
    }
}

// Shared so the runtime registration and the design-time factory can't drift on the
// history-table name (it must differ from AppDbContext's default __EFMigrationsHistory).
public static class ReadModelMigrations
{
    public const string HistoryTable = "__EFMigrationsHistory_ReadModel";
}
