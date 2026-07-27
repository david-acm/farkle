using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using HotDice.Ui;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.RegisterClientServices();

builder.Services.AddScoped(sp => new HttpClient
{
  BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

await builder.Build().RunAsync();
