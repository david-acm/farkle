using System.Reflection;
using HotDice.ApiClient;
using HotDice.Client.Api;
using HotDice.Shell.Lifecycle;
using Microsoft.Extensions.Logging;
using HotDice.Ui;

namespace HotDice.Shell;

public static class MauiProgram
{
    /// <summary>
    /// Unlike the web client — which inherits its origin from the host that served the WASM — a
    /// standalone app must be told where the backend is (docs/mobile-strategy.md, item 1).
    /// Override with HOTDICE_BACKEND_URL to point the app at a local or preview backend.
    /// </summary>
    public const string DefaultBackendUrl = "https://hotdice.davidacm.dev";

    /// <summary>
    /// The backend the app targets, resolved in priority order: the HOTDICE_BACKEND_URL env var
    /// (desktop/dev), then a value baked in at publish time via -p:HotDiceBackendUrl=… (the device
    /// tests point the app at a CI-local backend this way, since a device can't read host env vars),
    /// then the production default.
    /// </summary>
    internal static string ResolveBackendUrl()
    {
        var fromEnv = Environment.GetEnvironmentVariable("HOTDICE_BACKEND_URL");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;

        var baked = typeof(MauiProgram).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "HotDiceBackendUrl")?.Value;
        if (!string.IsNullOrWhiteSpace(baked)) return baked;

        return DefaultBackendUrl;
    }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();

        var backendUrl = ResolveBackendUrl();

        // The same UI the website runs, with the same registrations (RegisterClientServices brings
        // MudBlazor, BlazorState, the dice and the client services). The shell supplies only what a
        // standalone client cannot infer: an absolute backend address instead of a host origin.
        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(backendUrl) });
        builder.Services.RegisterClientServices();

        // Owns the HttpClient + Kiota adapter for the app's lifetime; the container disposes it
        // on shutdown (ADR 0010).
        builder.Services.AddSingleton(_ => new HotDiceApiConnection(backendUrl));
        builder.Services.AddSingleton<HotDiceApiClient>(sp => sp.GetRequiredService<HotDiceApiConnection>().Client);

        builder.Services.AddSingleton<MauiAppLifecycleBridge>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
