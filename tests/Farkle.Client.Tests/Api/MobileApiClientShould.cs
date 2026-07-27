using System.Net;
using Farkle.Client.Api;
using RichardSzalay.MockHttp;

namespace Farkle.Client.Tests.Api;

/// <summary>
/// Proves the mobile app's API path is unit-testable off-device: <see cref="FarkleApiConnection"/>
/// builds the generated Kiota client over an injected <see cref="HttpMessageHandler"/>, so a test
/// can drive a real request/response round trip with no server, no WebView and no device — and can
/// assert the absolute base address a standalone client needs (unlike the web app, which inherits
/// its origin from the host that served the WASM).
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

        using var connection = new FarkleApiConnection(BaseUrl, handler);

        var response = await connection.Client.Api.Games.PostAsync();

        response!.Id.Should().Be(992615);
        handler.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task SurfaceAServerFailureToTheCaller()
    {
        using var handler = new MockHttpMessageHandler();
        handler.When(HttpMethod.Post, $"{BaseUrl}/api/games")
               .Respond(HttpStatusCode.ServiceUnavailable);

        using var connection = new FarkleApiConnection(BaseUrl, handler);

        var call = async () => await connection.Client.Api.Games.PostAsync();

        await call.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void RejectARelativeBaseAddress()
    {
        // The web client can live on a relative origin; a standalone mobile app cannot. Failing
        // here beats failing as an opaque connection error on a device.
        using var handler = new MockHttpMessageHandler();

        var create = () => new FarkleApiConnection("/api", handler);

        create.Should().Throw<ArgumentException>().WithMessage("*absolute*");
    }

    [Fact]
    public void NotDisposeAHandlerItDoesNotOwn()
    {
        // HttpClient disposes its handler by default. The handler here belongs to the caller (a
        // test double, or a shared handler in the app), so disposing it would break the owner —
        // and would double-dispose once the caller's own `using` runs.
        using var callerOwned = new TrackingHandler();

        using (var connection = new FarkleApiConnection(BaseUrl, callerOwned))
        {
            connection.Client.Should().NotBeNull();
        }

        callerOwned.WasDisposed.Should().BeFalse();
    }

    [Fact]
    public void DisposeCleanlyWhenItOwnsTheWholeChain()
    {
        // No handler supplied → the connection creates and therefore owns the transport, so
        // disposing it must not throw and must be safe to repeat.
        var connection = new FarkleApiConnection(BaseUrl);

        var dispose = () => { connection.Dispose(); connection.Dispose(); };

        dispose.Should().NotThrow();
    }

    private sealed class TrackingHandler : HttpMessageHandler
    {
        public bool WasDisposed { get; private set; }

        // This handler exists only to observe disposal — the tests using it never issue a request,
        // so it manufactures no response to own. Throwing makes an unexpected send obvious instead
        // of silently passing.
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new NotSupportedException("TrackingHandler observes disposal; it does not send.");

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
