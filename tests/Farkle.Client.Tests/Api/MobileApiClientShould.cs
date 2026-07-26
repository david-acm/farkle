using System.Net;
using Farkle.ApiClient;
using Farkle.Client.Api;
using RichardSzalay.MockHttp;

namespace Farkle.Client.Tests.Api;

/// <summary>
/// Proves the mobile app's API path is unit-testable off-device: the shared factory builds the
/// generated Kiota client over an injected <see cref="HttpMessageHandler"/>, so a test can drive a
/// real request/response round trip with no server, no WebView and no device — and can assert the
/// absolute base address a standalone client needs (unlike the web app, which inherits its origin
/// from the host that served the WASM).
/// </summary>
public class MobileApiClientShould
{
    private const string BaseUrl = "https://hotdice.example.com";

    [Fact]
    public async Task SendRequestsToTheConfiguredAbsoluteBaseAddress()
    {
        using var handler = new MockHttpMessageHandler();
        handler.Expect(HttpMethod.Post, $"{BaseUrl}/api/games")
               .Respond("application/json", """{"id":992615}""");

        var client = FarkleApiClientFactory.Create(BaseUrl, handler);

        var response = await client.Api.Games.PostAsync();

        response!.Id.Should().Be(992615);
        handler.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task SurfaceAServerFailureToTheCaller()
    {
        using var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Post, $"{BaseUrl}/api/games")
               .Respond(HttpStatusCode.ServiceUnavailable);

        var client = FarkleApiClientFactory.Create(BaseUrl, handler);

        var call = async () => await client.Api.Games.PostAsync();

        await call.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void RejectARelativeBaseAddress()
    {
        // The web client can live on a relative origin; a standalone mobile app cannot. Failing
        // here beats failing as an opaque connection error on a device.
        using var handler = new MockHttpMessageHandler();

        var create = () => FarkleApiClientFactory.Create("/api", handler);

        create.Should().Throw<ArgumentException>().WithMessage("*absolute*");
    }
}
