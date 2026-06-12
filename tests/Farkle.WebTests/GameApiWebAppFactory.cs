using DotNet.Testcontainers.Builders;
using EventStore.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WebApp.Auth;

namespace Farkle.WebTests;

public class GameApiWebAppFactory : WebApplicationFactory<Program>
{
  private static Dictionary<string, string> Variables => new()
  {
    { "EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP", "true" },
    { "EVENTSTORE_INSECURE", "true" },
    { "EVENTSTORE_CLUSTER_SIZE", "1" },
    { "EVENTSTORE_EXT_TCP_PORT", "4113" },
    { "EVENTSTORE_HTTP_PORT", "5113" },
    { "EVENTSTORE_ENABLE_EXTERNAL_TCP", "true" },
    { "EVENTSTORE_RUN_PROJECTIONS", "all" },
    { "EVENTSTORE_START_STANDARD_PROJECTIONS", "true" },
    { "PATH", "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin" },
    { "ASPNETCORE_URLS", "http://+:80" },
    { "DOTNET_RUNNING_IN_CONTAINER", "true" }
  };

  protected override void ConfigureWebHost(IWebHostBuilder builder)
  {
    var path = Environment.GetEnvironmentVariable("PATH");
    Environment.SetEnvironmentVariable("PATH", path + ":/usr/local/bin");

    var esdbContainer = new ContainerBuilder("eventstore/eventstore:23.10.0-bookworm-slim")
      .WithPortBinding(4113, true)
      .WithPortBinding(5113, true)
      .WithEnvironment(Variables)
      .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5113)))
      .WithAutoRemove(false).Build();

    esdbContainer.StartAsync().GetAwaiter().GetResult();
    var esdbPort = esdbContainer.GetMappedPublicPort(5113);

    var pgContainer = new ContainerBuilder("postgres:16-alpine")
      .WithPortBinding(5432, true)
      .WithEnvironment("POSTGRES_USER", "farkle_test")
      .WithEnvironment("POSTGRES_PASSWORD", "farkle_test")
      .WithEnvironment("POSTGRES_DB", "farkle_test")
      // The postgres image starts a *temporary* server to run its init scripts, then
      // restarts the real one. `pg_isready` succeeds against that temp server, so tests
      // that connect in the gap get "connection reset by peer" when it restarts. Wait
      // until "ready to accept connections" is logged twice (temp + real) so we only
      // proceed once the durable server is up.
      .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilCommandIsCompleted("pg_isready", "-U", "farkle_test")
        .UntilMessageIsLogged("database system is ready to accept connections[\\s\\S]*database system is ready to accept connections"))
      .WithAutoRemove(false).Build();

    pgContainer.StartAsync().GetAwaiter().GetResult();
    var pgPort = pgContainer.GetMappedPublicPort(5432);

    base.ConfigureWebHost(builder);

    builder.ConfigureAppConfiguration(cfg =>
      cfg.AddInMemoryCollection(new Dictionary<string, string?>
      {
        ["Auth:RequireAuthorization"] = "true"
      }));

    builder.ConfigureServices(s =>
    {
      var esClient = s.First(s => s.ServiceType == typeof(EventStoreClient));
      s.Remove(esClient);
      s.AddEventStoreClient(TestEnvironment.EsdbConnectionString(esdbPort));

      var dbContextDescriptor = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
      if (dbContextDescriptor != null) s.Remove(dbContextDescriptor);

      var pgConnectionString = $"Host=localhost;Port={pgPort};Database=farkle_test;Username=farkle_test;Password=farkle_test";
      s.AddDbContext<AppDbContext>(o => o.UseNpgsql(pgConnectionString));
    });
  }
}
