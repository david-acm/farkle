using DotNet.Testcontainers.Builders;
using EventStore.Client;
using Farkle.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

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
    // TODO: add configuration to decide between local (commented code) and azure database
    // TODO: Change path to be read from the dotnet user secrets instead of it being hardcoded here
    var path     = Environment.GetEnvironmentVariable("PATH");
    ushort esdbPort = 5113;
    Environment.SetEnvironmentVariable("PATH", path + ":/usr/local/bin");
    new ContainerBuilder()
      .WithImage("ghcr.io/eventstore/eventstore:21.10.0-alpha-arm64v8")
      .WithPortBinding(4113)
      .WithPortBinding(esdbPort)
      .WithEnvironment(Variables)
      .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(esdbPort)))
      .WithAutoRemove(false).Build()
      .StartAsync()
      .GetAwaiter()
      .GetResult();

    base.ConfigureWebHost(builder);
    var webBuilder = builder.ConfigureServices(s =>
    {
      var esClient = s.First(s => s.ServiceType == typeof(EventStoreClient));
      s.Remove(esClient);
      var esdbTestConnectionString = $"esdb://admin:changeit@localhost:{esdbPort}?tls=false";
      s.AddEventStoreClient(esdbTestConnectionString);
    });
    
  }
}
