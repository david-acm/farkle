using DotNet.Testcontainers.Builders;
using Farkle.Infrastructure.Identity;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.WebTests;

// Boots the app on one Postgres (ADR 0004): Identity + Marten (events, GameState snapshot) + the
// Wolverine message store all share it. No ESDB container anymore. CI spins a Postgres Testcontainer;
// a developer without Docker can point at a local Postgres via FARKLE_TEST_PG (e.g. the SessionStart
// hook's instance) to run these without Docker.
public class GameApiWebAppFactory : FarkleWebApplicationFactory
{
  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    var localConn = Environment.GetEnvironmentVariable("FARKLE_TEST_PG");
    string pgConn;

    if (!string.IsNullOrWhiteSpace(localConn))
    {
      pgConn = localConn;
    }
    else
    {
      var pgContainer = new ContainerBuilder("postgres:16-alpine")
        .WithPortBinding(5432, true)
        .WithEnvironment("POSTGRES_USER", "farkle_test")
        .WithEnvironment("POSTGRES_PASSWORD", "farkle_test")
        .WithEnvironment("POSTGRES_DB", "farkle_test")
        // The postgres image runs its init scripts under a temporary unix-socket-only server, then
        // restarts with TCP. Force pg_isready over TCP (-h) so the wait only passes once the durable
        // TCP server is up (otherwise tests connecting in the gap get "connection reset by peer").
        .WithWaitStrategy(Wait.ForUnixContainer()
          .UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "farkle_test"))
        .WithAutoRemove(false).Build();

      pgContainer.StartAsync().GetAwaiter().GetResult();
      var pgPort = pgContainer.GetMappedPublicPort(5432);
      pgConn = $"Host=localhost;Port={pgPort};Database=farkle_test;Username=farkle_test;Password=farkle_test";
    }

    base.ConfigureWebHost(builder);

    builder.ConfigureAppConfiguration(cfg =>
      cfg.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Auth:RequireAuthorization"] = "true"
      }));

    // Point both stores at the test Postgres. AddFarkleIdentity/AddFarkleCritterStack capture their
    // connection at registration (from a config value that's unset here), so override the services
    // directly: re-register the Identity DbContext, and layer a Marten Connection() (last one wins).
    builder.ConfigureServices(s =>
    {
      var dbDesc = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
      if (dbDesc != null) s.Remove(dbDesc);
      s.AddDbContext<AppDbContext>(o => o.UseNpgsql(pgConn));

      s.ConfigureMarten(opts => opts.Connection(pgConn));
    });
  }
}
