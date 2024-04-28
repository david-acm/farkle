using BlazorState;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using WebApp.Client.Services;

namespace WebApp.Client;

public static class ClientServiceExtensions
{
  public static void RegisterClientServices(this IServiceCollection services) 
  {
    services.AddMudServices();
    services.AddSingleton<IRotationCalculator, RotationCalculator>();
    services.AddScoped<IGameService, GameService>();
    
    var assembly = typeof(Program).Assembly;
    services.AddBlazorState(o => o.Assemblies = [assembly]);
  }
}
