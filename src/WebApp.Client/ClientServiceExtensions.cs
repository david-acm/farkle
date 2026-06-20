using BlazorState;
using Blazor.Dice;
using MediatR;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using MudBlazor.Services;
using WebApp.Client.Services;
using WebApp.Client.Telemetry;
using Farkle.ApiClient;

namespace WebApp.Client;

public static class ClientServiceExtensions
{
  public static void RegisterClientServices(this IServiceCollection services)
  {
    services.AddMudServices();
    services.AddBlazorDice();
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
    services.AddScoped<IGameHubService, GameHubService>();
    services.AddScoped<IShareService, ShareService>();
    services.AddScoped<IUiTelemetry, UiTelemetry>();

    var assembly = typeof(Program).Assembly;
    services.AddBlazorState(o => o.Assemblies = [assembly]);

    // #225 — emit a UI intent custom event for every dispatched action. Registered after
    // AddBlazorState so it runs closest to the handler (it reads post-action state). Open-generic,
    // so it wraps current and future actions without per-handler wiring.
    services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UiTelemetryBehavior<,>));
  }
}
