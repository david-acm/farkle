using BlazorState;
using Farkle.Spa;
using Farkle.Spa.Components;
using Farkle.Spa.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();

builder.Services.AddSingleton<IRotationCalculator, RotationCalculator>();
builder.Services.AddScoped<IGameService, GameService>();
builder.Services.AddScoped(sp => new HttpClient
{
  BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

var assembly = typeof(Program).Assembly;
builder.Services.AddBlazorState(o => o.Assemblies = [assembly]);

await builder.Build().RunAsync();

namespace Farkle.Spa
{
public partial class Program
{
  
}
}
