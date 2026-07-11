using System.Threading;
using Alba;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace Farkle.WebTests.Harness;

// Shared integration host for the Critter-way test harness (#304). Boots the real Program once per
// test collection as an Alba IAlbaHost — the Critter Stack testing entrypoint — against one Postgres
// (a Testcontainer in CI, or FARKLE_TEST_PG locally, e.g. the SessionStart hook's instance). This
// replaces the per-class WebApplicationFactory: one host per collection instead of one per test class.
//
// External Wolverine transports are disabled (see FarkleTestHost) so the endpoints' cascaded broadcast
// notifications stay in-process and a TrackedSession can wait on them deterministically (no sleeps /
// polling). Marten runs in an isolated schema so ResetAllData between tests only clears this host's
// data — letting a second fixture (the scripted-id variant) share the same physical database.
public class AppFixture : IAsyncLifetime
{
  private IContainer? _postgres;

  public IAlbaHost Host { get; private set; } = null!;

  // The resolved Postgres connection, exposed so a test that needs its own throwaway host (the codegen
  // type-check) can reuse this database instead of spinning another container.
  public string ConnectionString { get; private set; } = string.Empty;

  // Distinct Marten schema per fixture so two integration hosts sharing one Postgres (a developer's
  // FARKLE_TEST_PG) don't clobber each other's streams on ResetAllData.
  protected virtual string MartenSchema => "farkle_it";

  // Hook for a derived fixture to add/replace services after the standard test wiring (e.g. a
  // scripted IGameIdGenerator to drive the collision-retry path).
  protected virtual void ConfigureTestServices(IServiceCollection services) { }

  public async Task InitializeAsync()
  {
    ConnectionString = await ResolvePostgresAsync();
    Host = await FarkleTestHost.StartAsync(ConnectionString, MartenSchema, ConfigureTestServices);
  }

  // CI runs without Docker-in-the-hook, so spin a throwaway Postgres Testcontainer; a developer (or a
  // session with the Postgres SessionStart hook) points FARKLE_TEST_PG at an existing instance.
  private async Task<string> ResolvePostgresAsync()
  {
    var local = Environment.GetEnvironmentVariable("FARKLE_TEST_PG");
    if (!string.IsNullOrWhiteSpace(local)) return local;

    var container = new ContainerBuilder("postgres:16-alpine")
      .WithPortBinding(5432, true)
      .WithEnvironment("POSTGRES_USER", "farkle_test")
      .WithEnvironment("POSTGRES_PASSWORD", "farkle_test")
      .WithEnvironment("POSTGRES_DB", "farkle_test")
      // The postgres image runs its init scripts under a temporary unix-socket-only server, then
      // restarts with TCP. Force pg_isready over TCP (-h) so the wait passes only once the durable
      // TCP server is up (otherwise a connecting test gets "connection reset by peer").
      .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "farkle_test"))
      .WithAutoRemove(true).Build();

    await container.StartAsync();
    _postgres = container;
    var port = container.GetMappedPublicPort(5432);
    return $"Host=localhost;Port={port};Database=farkle_test;Username=farkle_test;Password=farkle_test";
  }

  // Marten data isolation between tests (#304). Only clears this host's schema, so a parallel
  // collection fixture on the same database is untouched.
  public Task ResetAsync() =>
    Host.Services.GetRequiredService<IDocumentStore>().Advanced.ResetAllData(CancellationToken.None);

  public async Task DisposeAsync()
  {
    await FarkleTestHost.StopTolerantlyAsync(Host);
    if (_postgres is not null) await _postgres.DisposeAsync();
  }
}
