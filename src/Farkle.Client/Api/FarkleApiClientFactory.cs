using System.Text;
using Farkle.ApiClient;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace Farkle.Client.Api;

/// <summary>
/// Builds the generated Kiota client against an absolute backend URL.
/// </summary>
/// <remarks>
/// The web client inherits its origin from the host that served the WASM; a standalone mobile app
/// has no such host and must be told where the backend is. Taking the <see cref="HttpMessageHandler"/>
/// as a parameter is what lets tests drive the real client over a fake transport, with no server.
/// </remarks>
public static class FarkleApiClientFactory
{
    public static FarkleApiClient Create(string baseUrl, HttpMessageHandler? handler = null)
    {
        // The scheme check is not belt-and-braces: on Unix `Uri.TryCreate("/api", Absolute, …)`
        // succeeds as a *file* URI, so requiring absoluteness alone would let a relative path
        // through and fail later as an opaque connection error on a device.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException(
                $"The backend URL must be an absolute http(s) URL for a standalone client (got '{baseUrl}').",
                nameof(baseUrl));

        var http = new HttpClient(new EmptyBodyJsonHandler(handler ?? new HttpClientHandler()))
        {
            BaseAddress = baseUri,
        };

        var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: http)
        {
            BaseUrl = baseUri.ToString().TrimEnd('/'),
        };

        return new FarkleApiClient(adapter);
    }

    /// <summary>
    /// Kiota emits bodyless POSTs for parameterless commands; ASP.NET rejects those without a JSON
    /// content type, so give them an empty object.
    /// </summary>
    private sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post && request.Content is null)
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            return base.SendAsync(request, ct);
        }
    }
}
