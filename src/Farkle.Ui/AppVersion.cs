using System.Reflection;

namespace Farkle.Ui;

// The WASM client build's version. Stamped via /p:InformationalVersion at publish time
// (the same global property stamps the WebApp.Client host assembly at publish). Local builds
// fall back to the assembly version.
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}
