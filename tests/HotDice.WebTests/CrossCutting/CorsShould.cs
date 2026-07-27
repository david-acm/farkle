using HotDice.WebTests.Harness;

namespace HotDice.WebTests.CrossCutting;

// Issue #28 — CORS preflight. An OPTIONS request from an allowed origin must come back with the
// matching Access-Control-Allow-Origin header. Allowed origins are read from Cors:AllowedOrigins
// (Development includes the localhost dev origins). Preflight is handled by the CORS middleware
// before auth, so it works even though the host runs with Auth:RequireAuthorization = true. This is
// a transport-level check with no Kiota surface, so it drives the raw TestServer client directly.
[Collection(IntegrationCollection.Name)]
public class CorsShould(AppFixture fixture)
{
  private const string AllowedOrigin = "http://localhost:5157";

  [Fact]
  public async Task AnswerAPreflightFromAnAllowedOriginWithCorsHeaders()
  {
    var client = fixture.Host.Server.CreateClient();

    using var request = new HttpRequestMessage(HttpMethod.Options, "/api/games");
    request.Headers.Add("Origin", AllowedOrigin);
    request.Headers.Add("Access-Control-Request-Method", "POST");

    using var response = await client.SendAsync(request);

    Assert.Equal(AllowedOrigin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
  }
}
