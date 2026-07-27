using Blazor.Dice;
using Farkle.Client.Api;
using Farkle.Client.Lifecycle;
using Farkle.ApiClient;
using HotDice.Shell.Lifecycle;
using Microsoft.Extensions.Logging;

namespace HotDice.Shell;

public static class MauiProgram
{
    /// <summary>
    /// Unlike the web client — which inherits its origin from the host that served the WASM — a
    /// standalone app must be told where the backend is (docs/mobile-strategy.md, item 1).
    /// Overridden per environment in a later step; the default points at the deployed app.
    /// </summary>
    public const string DefaultBackendUrl = "https://hotdice.davidacm.dev";

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"));

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddBlazorDice();

        // Everything below here is the shared, desktop-testable core (ADR 0010) — the shell only
        // supplies the absolute backend URL and the window-event bridge.
        var backendUrl = Environment.GetEnvironmentVariable("HOTDICE_BACKEND_URL") ?? DefaultBackendUrl;
        // Registered as a singleton so the container owns the HttpClient's lifetime and disposes
        // it on shutdown; the client itself is resolved from it.
        builder.Services.AddSingleton(_ => new FarkleApiConnection(backendUrl));
        builder.Services.AddSingleton<FarkleApiClient>(sp => sp.GetRequiredService<FarkleApiConnection>().Client);
        builder.Services.AddSingleton<MauiAppLifecycleBridge>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
