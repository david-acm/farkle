using System.Net;
using System.Net.Http;

namespace Farkle.WebTests;

// Issue #28 — CORS preflight. An OPTIONS request from an allowed origin must come
// back with the matching Access-Control-Allow-Origin header. Allowed origins are
// read from Cors:AllowedOrigins in configuration (Development includes the
// localhost dev origins). Preflight is handled by the CORS middleware before auth,
// so it works even though this host runs with Auth:RequireAuthorization = true.
public class CorsShould(GameApiWebAppFactory factory) : IClassFixture<GameApiWebAppFactory>
{
    // Present in appsettings.Development.json's Cors:AllowedOrigins (tests run in
    // the Development environment).
    private const string AllowedOrigin = "http://localhost:5157";

    [Fact]
    public async Task Answer_a_preflight_from_an_allowed_origin_with_cors_headers()
    {
        var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/games");
        request.Headers.Add("Origin", AllowedOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(
            AllowedOrigin,
            response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }
}
