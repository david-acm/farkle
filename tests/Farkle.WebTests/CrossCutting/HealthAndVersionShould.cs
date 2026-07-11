using System.Net;
using Farkle.WebTests.Harness;

namespace Farkle.WebTests.CrossCutting;

// Issues #25 / build-version endpoint. Health and version endpoints are mapped before auth so they
// answer anonymously even though the host runs with Auth:RequireAuthorization = true. Readiness
// reports the Postgres check (which also covers Marten's event store — it shares the database, ADR
// 0004). Transport-level checks with no Kiota surface, so they drive the raw TestServer client.
[Collection(IntegrationCollection.Name)]
public class HealthAndVersionShould(AppFixture fixture)
{
  [Fact]
  public async Task ServeLivenessAnonymously()
  {
    var client = fixture.Host.Server.CreateClient();
    using var response = await client.GetAsync("/health/live");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task ReportReadyWhenPostgresIsUp()
  {
    var client = fixture.Host.Server.CreateClient();
    using var response = await client.GetAsync("/health/ready");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task ExposeTheBuildVersionAnonymously()
  {
    var client = fixture.Host.Server.CreateClient();

    using var response = await client.GetAsync("/version");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    using var doc = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    Assert.False(string.IsNullOrWhiteSpace(doc.RootElement.GetProperty("version").GetString()));
  }
}
