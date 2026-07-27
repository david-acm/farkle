using System.Text;
using HotDice.ApiClient;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace HotDice.Client.Api;

/// <summary>
/// Owns the transport behind the generated Kiota client for one backend.
/// </summary>
/// <remarks>
/// The web client inherits its origin from the host that served the WASM; a standalone mobile app
/// has none and must be told where the backend is, so the URL is required and must be absolute.
///
/// This is a *connection*, not a static factory, because something has to own the HttpClient and
/// its handler chain: they live for the app's lifetime (register it as a singleton and let the
/// container dispose it), and calling this per request would leak sockets. Taking the
/// <see cref="HttpMessageHandler"/> as a parameter is what lets tests drive the real client over a
/// fake transport with no server.
/// </remarks>
public sealed class HotDiceApiConnection : IDisposable
{
    private readonly HttpClient               _http;
    private readonly HttpClientRequestAdapter _adapter;
    private          bool                     _disposed;

    public HotDiceApiConnection(string baseUrl, HttpMessageHandler? handler = null)
    {
        // The scheme check is not belt-and-braces: on Unix `Uri.TryCreate("/api", Absolute, …)`
        // succeeds as a *file* URI, so requiring absoluteness alone would let a relative path
        // through and fail later as an opaque connection error on a device.
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException(
                $"The backend URL must be an absolute http(s) URL for a standalone client (got '{baseUrl}').",
                nameof(baseUrl));

        // A caller-supplied handler belongs to the caller — disposing it here would break its owner
        // (and double-dispose once their own `using` runs). We only dispose the one we create.
        var ownsTransport = handler is null;

        _http = new HttpClient(new EmptyBodyJsonHandler(handler ?? new HttpClientHandler()), ownsTransport)
        {
            BaseAddress = baseUri,
        };

        _adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: _http)
        {
            BaseUrl = baseUri.ToString().TrimEnd('/'),
        };

        Client = new HotDiceApiClient(_adapter);
    }

    public HotDiceApiClient Client { get; }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        _adapter.Dispose();
        _http.Dispose();
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
