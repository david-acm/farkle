using System.Net;
using DotNet.Testcontainers.Builders;
using Farkle.Domain.GameAggregate;
using Farkle.Infrastructure.Identity;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Farkle.E2eTests;

// Boots the real WebApp host (serving the WASM client) on a dynamic Kestrel port so Playwright can
// drive it. Post-cutover (ADR 0004) the write path is Marten + Wolverine on Postgres, so the
// storyboard needs a real Postgres — no more in-memory aggregate store. It spins a lightweight
// Postgres Testcontainer (or reuses FARKLE_TEST_PG, e.g. the SessionStart hook's instance), keeps the
// game endpoints anonymous, and swaps IRandom for a deterministic ScriptedRandom.
//
// This is the "light" companion to E2EWebAppFactory: the heavyweight happy-path tests use one factory,
// the storyboard capture uses this one. xUnit only instantiates the fixture whose tests are selected.
public sealed class StoryboardWebAppFactory : WebApplicationFactory<Program>
{
    public string ServerAddress { get; private set; } = string.Empty;

    private IHost? _kestrelHost;

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
                .WithEnvironment("POSTGRES_USER", "farkle_story")
                .WithEnvironment("POSTGRES_PASSWORD", "farkle_story")
                .WithEnvironment("POSTGRES_DB", "farkle_story")
                .WithWaitStrategy(Wait.ForUnixContainer()
                    .UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "farkle_story"))
                .WithAutoRemove(false).Build();

            pgContainer.StartAsync().GetAwaiter().GetResult();
            var pgPort = pgContainer.GetMappedPublicPort(5432);
            pgConn = $"Host=localhost;Port={pgPort};Database=farkle_story;Username=farkle_story;Password=farkle_story";
        }

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // The storyboard never logs in, so skip the Identity migrate/seed at startup.
                ["Storyboard:SkipIdentitySeed"] = "true",
                // Keep the game endpoints anonymous so the UI renders without logging in.
                ["Auth:RequireAuthorization"]   = "false",
                // #303 — the storyboard drives a single browser (no live SignalR broadcasts to
                // spectators), so run Wolverine mediator-only. This factory builds two hosts (the
                // test client + the Kestrel host for Playwright); full durability would start two
                // Wolverine nodes racing on leadership over one Postgres — the source of a flaky
                // BackendStubShould boot error. Mediator-only removes the agents (broadcasts, unused
                // here, still deliver in-process).
                ["Farkle:LightweightWolverine"]  = "true",
            });
        });

        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // AddFarkleCritterStack captured its connection at registration (from unset config), so
            // layer the test Postgres onto Marten (last Connection() wins).
            services.ConfigureMarten(opts => opts.Connection(pgConn));

            // Deterministic dice via the DI seam (#93): swap the default IRandom for ScriptedRandom
            // (always rolls the minimum → six 1s) so every roll yields a scoring die and the
            // storyboard stages stay reproducible.
            services.RemoveAll<IRandom>();
            services.AddSingleton<IRandom, ScriptedRandom>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the in-memory test host WebApplicationFactory uses internally.
        var testHost = builder.Build();

        // Build a real Kestrel host on a dynamic loopback port for Playwright.
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
        // Two hosts share one Marten/Wolverine store; racing their shutdown can surface an
        // ObjectDisposedException (sometimes wrapped in an AggregateException) from the background
        // durability agent. That's harmless teardown noise — swallow ODE / AggregateException-of-ODE
        // so it never fails the collection-fixture cleanup; rethrow anything else.
        if (_kestrelHost is not null)
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
