using FastEndpoints;
using FluentAssertions;

namespace Farkle.WebTests;

public static class HttpClientExtensions
{
  public static async Task<TResponse> PostAndEnsureOkAsync<TEndpoint, TRequest, TResponse>(
    this HttpClient client,
    TRequest        body)
  where TRequest : notnull
  where TEndpoint : IEndpoint
  {
    var (rsp, res) = await client.POSTAsync<TEndpoint, TRequest, TResponse>(body);
    
    rsp.IsSuccessStatusCode.Should()
      .BeTrue($"Status code returned was: {rsp.StatusCode}, with reason: {rsp.ReasonPhrase} {res}");
    
    return res;
  }
}
