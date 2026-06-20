using System.Net;
using DotNet.Testcontainers.Builders;
using EventStore.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Farkle.Infrastructure.Identity;

namespace Farkle.E2eTests;

public class E2EWebAppFactory : WebApplicationFactory<Program>
{
    public string ServerAddress { get; private set; } = string.Empty;

    // EventStore runs in insecure mode in the test container; these are its
    // well-known default credentials, named rather than inlined.
    private const string EsdbUser     = "admin";
    private const string EsdbPassword = "changeit";

    private IHost?                  _kestrelHost;
    private readonly InMemoryLoggerProvider _logProvider = new();

    /// <summary>Removes and returns all buffered API log entries since the last drain.</summary>
    public IReadOnlyList<string> DrainApiLogs() => _logProvider.Drain();

    private static Dictionary<string, string> EsdbVariables => new()
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
            .WithEnvironment(EsdbVariables)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(5113)))
            .WithAutoRemove(false).Build();

        esdbContainer.StartAsync().GetAwaiter().GetResult();
        var esdbPort = esdbContainer.GetMappedPublicPort(5113);

        var pgContainer = new ContainerBuilder("postgres:16-alpine")
            .WithPortBinding(5432, true)
            .WithEnvironment("POSTGRES_USER", "farkle_e2e")
            .WithEnvironment("POSTGRES_PASSWORD", "farkle_e2e")
            .WithEnvironment("POSTGRES_DB", "farkle_e2e")
            // TCP pg_isready (-h): the postgres image's temporary init server listens only
            // on the unix socket, so a socket-based check passes too early and connections
            // race the restart ("connection reset by peer"). Wait for the real TCP server.
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "farkle_e2e"))
            .WithAutoRemove(false).Build();

        pgContainer.StartAsync().GetAwaiter().GetResult();
        var pgPort = pgContainer.GetMappedPublicPort(5432);

        base.ConfigureWebHost(builder);

        // Capture API logs so they can be written to disk when a test fails.
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(_logProvider);
            // Suppress EF Core query noise; keep application + ASP.NET at Information.
            logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore",          LogLevel.Information);
        });

        builder.ConfigureServices(s =>
        {
            var esClient = s.First(d => d.ServiceType == typeof(EventStoreClient));
            s.Remove(esClient);
            s.AddEventStoreClient($"esdb://{EsdbUser}:{EsdbPassword}@localhost:{esdbPort}?tls=false");

            var pgConnectionString = $"Host=localhost;Port={pgPort};Database=farkle_e2e;Username=farkle_e2e;Password=farkle_e2e";

            var dbDescriptor = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) s.Remove(dbDescriptor);
            s.AddDbContext<AppDbContext>(o => o.UseNpgsql(pgConnectionString));

            // Read model (#156) shares the same Postgres — point it at the e2e container too
            // (own history table), so its migration applies and the projector can write.
            var readDescriptor = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<Farkle.Infrastructure.ReadModel.ReadModelDbContext>));
            if (readDescriptor != null) s.Remove(readDescriptor);
            s.AddDbContext<Farkle.Infrastructure.ReadModel.ReadModelDbContext>(o =>
                o.UseNpgsql(pgConnectionString, b => b.MigrationsHistoryTable(Farkle.Infrastructure.ReadModel.ReadModelMigrations.HistoryTable)));
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the in-memory test host that WebApplicationFactory uses internally.
        var testHost = builder.Build();

        // Build a second host with real Kestrel on a dynamic port for Playwright.
        builder.ConfigureWebHost(b => b.UseKestrel(o => o.Listen(IPAddress.Loopback, 0)));
        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var server    = _kestrelHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()!;
        ServerAddress = addresses.Addresses.First();

        return testHost;
    }

    public override async ValueTask DisposeAsync()
    {
        // Both hosts run the broadcast subscription, whose shutdown trips a known Eventuous bug
        // (CheckpointCommitHandler double-disposes a CancellationTokenSource). Host.StopAsync
        // surfaces it *wrapped in an AggregateException*, so swallowing only ObjectDisposedException
        // let it escape and fail the collection-fixture cleanup — failing the whole run despite the
        // tests passing. Swallow ODE and an AggregateException whose inners are all ODE; rethrow
        // anything else.
        if (_kestrelHost != null)
        {
            try { await _kestrelHost.StopAsync(); } catch (Exception ex) when (IsHarmlessTeardown(ex)) { }
            _kestrelHost.Dispose();
        }

        try { await base.DisposeAsync(); } catch (Exception ex) when (IsHarmlessTeardown(ex)) { }
    }

    private static bool IsHarmlessTeardown(Exception ex) =>
        ex is ObjectDisposedException
        || (ex is AggregateException agg && agg.Flatten().InnerExceptions.All(e => e is ObjectDisposedException));
}
