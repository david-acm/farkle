using System.Text;

namespace Farkle.Ui;

internal sealed class EmptyBodyJsonHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
  {
    if (request.Method == HttpMethod.Post && request.Content == null)
      request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
    return base.SendAsync(request, ct);
  }
}
