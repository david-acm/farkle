using System.Net;

namespace Farkle.WebTests;

// Issue #25 — health endpoints. They must be mapped before auth so they answer
// anonymously even though this factory runs with Auth:RequireAuthorization = true,
// and /health/ready must report readiness of Postgres + EventStore (both are up via
// Testcontainers here). Runs in CI (Docker), like the other GameApiWebAppFactory tests.
public class HealthChecksShould(GameApiWebAppFactory factory) : IClassFixture<GameApiWebAppFactory>
{
    [Fact]
    public async Task Serve_liveness_anonymously()
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Report_ready_when_postgres_and_eventstore_are_up()
    {
        var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
