using System.Net;
using EventStore.Client;
using Eventuous;
using Eventuous.EventStore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Farkle.StoryboardTests;

// Boots the real WebApp host (serving the WASM client) on a dynamic Kestrel port so
// Playwright can drive it — but with the ESDB-backed aggregate store swapped for an
// in-memory one and the Identity migrate/seed skipped. No Testcontainers, no Docker.
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
      RemoveAll(services, typeof(IAggregateStore));
      RemoveAll(services, typeof(IEventStore));
      RemoveAll(services, typeof(EsdbEventStore));
      RemoveAll(services, typeof(EventStoreClient));

      services.AddSingleton<IAggregateStore, InMemoryAggregateStore>();
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
