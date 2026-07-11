using System.Net;
using DotNet.Testcontainers.Builders;
using Marten;
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

    private IHost?                  _kestrelHost;
    private readonly InMemoryLoggerProvider _logProvider = new();

    /// <summary>Removes and returns all buffered API log entries since the last drain.</summary>
    public IReadOnlyList<string> DrainApiLogs() => _logProvider.Drain();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", path + ":/usr/local/bin");

        // One Postgres for the whole app (ADR 0004): Identity + Marten (events + GameState snapshot)
        // + the Wolverine message store. No ESDB anymore. FARKLE_TEST_PG lets a developer without
        // Docker point at a local Postgres (e.g. the SessionStart hook's instance).
        var localConn = Environment.GetEnvironmentVariable("FARKLE_TEST_PG");
        string pgConnectionString;

        if (!string.IsNullOrWhiteSpace(localConn))
        {
            pgConnectionString = localConn;
        }
        else
        {
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
            pgConnectionString = $"Host=localhost;Port={pgPort};Database=farkle_e2e;Username=farkle_e2e;Password=farkle_e2e";
        }

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
            // AddFarkleIdentity/AddFarkleCritterStack capture their connection at registration (from a
            // config value that's unset here), so override the services: re-register the Identity
            // DbContext and layer a Marten Connection() (last one wins).
            var dbDescriptor = s.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbDescriptor != null) s.Remove(dbDescriptor);
            s.AddDbContext<AppDbContext>(o => o.UseNpgsql(pgConnectionString));

            s.ConfigureMarten(opts => opts.Connection(pgConnectionString));
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
        // Two hosts (in-memory + Kestrel) share one Marten/Wolverine store; racing their shutdown can
        // surface an ObjectDisposedException (sometimes wrapped in an AggregateException) from the
        // background durability agent. That's harmless teardown noise — swallow ODE and an
        // AggregateException whose inners are all ODE; rethrow anything else.
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
