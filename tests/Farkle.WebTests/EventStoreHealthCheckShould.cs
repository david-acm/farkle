using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using WebApp.Health;

namespace Farkle.WebTests;

// Unit test (no Testcontainers) — runs locally and in the unit CI step. Guards the
// storyboard-host contract: StoryboardWebAppFactory removes EventStoreClient from DI,
// so the check must report Unhealthy WITHOUT throwing when the client is absent
// (otherwise that host's container validation would fail to build).
public class EventStoreHealthCheckShould
{
    [Fact]
    public async Task Report_unhealthy_without_throwing_when_client_is_not_registered()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var check = new EventStoreHealthCheck(provider);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}
