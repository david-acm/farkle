namespace WebApp.Client;

/// <summary>
/// Ensures POST requests with no body still carry Content-Type: application/json,
/// which FastEndpoints requires to bind the request correctly.
/// </summary>
internal sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method == HttpMethod.Post && request.Content is null)
            request.Content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        return base.SendAsync(request, cancellationToken);
    }
}
