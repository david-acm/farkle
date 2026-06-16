using System.Net;
using EventStore.Client;
using Eventuous;
using Eventuous.EventStore;
using Farkle.Domain.GameAggregate;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Farkle.E2eTests;

// Boots the real WebApp host (serving the WASM client) on a dynamic Kestrel port so
// Playwright can drive it — but with the ESDB-backed aggregate store swapped for an
// in-memory one and the Identity migrate/seed skipped. No Testcontainers, no Docker.
//
// This is the "light" companion to E2EWebAppFactory: the heavyweight happy-path tests
// use the container-backed factory, the storyboard capture uses this one. xUnit only
// instantiates the fixture whose tests are selected, so a filtered storyboard run never
// starts containers.
public sealed class StoryboardWebAppFactory : WebApplicationFactory<Program>
{
    public string ServerAddress { get; private set; } = string.Empty;

    private IHost? _kestrelHost;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Skip the Postgres migrate/seed at startup (game endpoints never touch Identity).
                ["Storyboard:SkipIdentitySeed"] = "true",
                // Keep the game endpoints anonymous so the UI renders without logging in.
                ["Auth:RequireAuthorization"]   = "false",
                // No EventStore here (in-memory aggregate store), so disable the broadcast
                // subscription — it needs the EventStoreClient and would fail host startup.
                ["Farkle:RealtimeBroadcastEnabled"] = "false",
                // Dummy connection strings: present so option binding succeeds, never contacted.
                ["ConnectionStrings:Identity"]  = "Host=localhost;Database=storyboard;Username=u;Password=p",
                ["ConnectionStrings:Esdb"]      = "esdb://localhost:2113?tls=false",
            });
        });

        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Replace the ESDB persistence with the in-memory store; drop the now-unused
            // EventStore wiring (including the EsdbEventStore singleton, registered as its
            // own concrete type) so DI validation doesn't try to reach a real database.
            // 0.15.1 registers the store as IEventStore/IEventReader/IEventWriter (AddEventStore)
            // rather than the retired IAggregateStore.
            RemoveAll(services, typeof(IEventStore));
            RemoveAll(services, typeof(IEventReader));
            RemoveAll(services, typeof(IEventWriter));
            RemoveAll(services, typeof(EsdbEventStore));
            RemoveAll(services, typeof(EventStoreClient));

            // Drop the broadcast subscription's hosted service — with no EventStoreClient it
            // would NRE on startup. Eventuous registers it as the only *factory-based*
            // IHostedService (no ImplementationType); the framework's own hosted services
            // (health-check publisher, data protection, the web host) all have concrete types.
            // (A config gate can't reach here: AddFarkleModuleServices reads config before the
            // test's ConfigureAppConfiguration is merged.)
            foreach (var hosted in services
                         .Where(d => d.ServiceType == typeof(IHostedService)
                                     && d.ImplementationFactory is not null
                                     && d.ImplementationType is null)
                         .ToList())
                services.Remove(hosted);

            var inMemoryStore = new InMemoryAggregateStore();
            services.AddSingleton<IEventStore>(inMemoryStore);
            services.AddSingleton<IEventReader>(inMemoryStore);
            services.AddSingleton<IEventWriter>(inMemoryStore);

            // 0.15.1 builds aggregates via AggregateFactoryRegistry rather than the store's own
            // factory. Register Game with the deterministic ScriptedRandom on the global registry
            // (the default the CommandService + LoadAggregate use) so every roll yields scoring
            // dice and the storyboard stages stay reproducible.
            AggregateFactoryRegistry.Instance
                .CreateAggregateUsing<Game, GameState>(() => new Game(new ScriptedRandom()));

            // The read model (#156) is gated on config that isn't merged yet at registration
            // time (same limitation as the broadcast subscription above), so Program registers
            // the Postgres-backed EfGameViewStore here too. Swap it for the no-op store so GET
            // falls back to the in-memory replay instead of hitting the dummy Postgres. The
            // projector's subscription was already dropped by the hosted-service removal above.
            RemoveAll(services, typeof(Farkle.Application.IGameViewStore));
            services.AddSingleton<Farkle.Application.IGameViewStore, Farkle.Application.NullGameViewStore>();
        });
    }

    // Removes every registration that exposes or implements the given type.
    private static void RemoveAll(IServiceCollection services, Type type)
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType == type || d.ImplementationType == type)
                     .ToList())
            services.Remove(descriptor);
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
        if (_kestrelHost is not null)
        {
            await _kestrelHost.StopAsync();
            _kestrelHost.Dispose();
        }

        await base.DisposeAsync();
    }
}
