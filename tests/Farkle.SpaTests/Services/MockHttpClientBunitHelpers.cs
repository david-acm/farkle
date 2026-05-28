using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using RichardSzalay.MockHttp;
using Farkle.ApiClient;

namespace Farkle.SpaTests.Services;

public static class MockHttpClientBUnitHelpers
{
  private static readonly JsonSerializerOptions _camelCaseOptions =
    new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

  public static MockHttpMessageHandler AddMockHttpClient(this IServiceCollection services)
  {
    var mockHttpHandler = new MockHttpMessageHandler();
    var httpClient      = mockHttpHandler.ToHttpClient();
    httpClient.BaseAddress = new Uri("http://localhost");
    services.AddSingleton<HttpClient>(httpClient);
    return mockHttpHandler;
  }

  public static MockHttpMessageHandler GetMockHttpClient()
  {
    var mockHttpHandler = new MockHttpMessageHandler();
    return mockHttpHandler;
  }

  public static FarkleApiClient ToFarkleApiClient(this MockHttpMessageHandler mock, string baseUrl = "http://localhost")
  {
    var httpClient = mock.ToHttpClient();
    httpClient.BaseAddress = new Uri(baseUrl);
    var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
    adapter.BaseUrl = baseUrl.TrimEnd('/');
    return new FarkleApiClient(adapter);
  }

  public static MockedRequest RespondJson<T>(this MockedRequest request, T content)
  {
    request.Respond(req =>
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK);
      response.Content                     = new StringContent(JsonSerializer.Serialize(content, _camelCaseOptions));
      response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
      return response;
    });
    return request;
  }

  public static MockedRequest RespondJson<T>(this MockedRequest request, Func<T> contentProvider)
  {
    request.Respond(req =>
    {
      var response = new HttpResponseMessage(HttpStatusCode.OK);
      response.Content                     = new StringContent(JsonSerializer.Serialize(contentProvider(), _camelCaseOptions));
      response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
      return response;
    });
    return request;
  }
}
