using BlazorState;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using MudBlazor.Services;
using WebApp.Client.Services;
using WebApp.Client.Services.Generated;

namespace WebApp.Client;

public static class ClientServiceExtensions
{
  public static void RegisterClientServices(this IServiceCollection services)
  {
    services.AddMudServices();
    services.AddSingleton<IRotationCalculator, RotationCalculator>();
    services.AddScoped<FarkleApiClient>(sp =>
    {
      var baseAddress = sp.GetRequiredService<HttpClient>().BaseAddress;
      var httpClient  = new HttpClient(new EmptyBodyJsonHandler(new HttpClientHandler()))
      {
        BaseAddress = baseAddress
      };
      var adapter = new HttpClientRequestAdapter(new AnonymousAuthenticationProvider(), httpClient: httpClient);
      adapter.BaseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
      return new FarkleApiClient(adapter);
    });
    services.AddScoped<IGameService, GameService>();

    var assembly = typeof(Program).Assembly;
    services.AddBlazorState(o => o.Assemblies = [assembly]);
  }
}
