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
using WebApp.Auth;

namespace Farkle.E2eTests;

public class E2EWebAppFactory : WebApplicationFactory<Program>
{
    public string ServerAddress { get; private set; } = string.Empty;

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
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-U", "farkle_e2e"))
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
            s.AddEventStoreClient($"esdb://admin:changeit@localhost:{esdbPort}?tls=false");

            var dbDescriptor = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) s.Remove(dbDescriptor);
            s.AddDbContext<AppDbContext>(o =>
                o.UseNpgsql($"Host=localhost;Port={pgPort};Database=farkle_e2e;Username=farkle_e2e;Password=farkle_e2e"));
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
        if (_kestrelHost != null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
        }

        await base.DisposeAsync();
    }
}
