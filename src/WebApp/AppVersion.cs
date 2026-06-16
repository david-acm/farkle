using System.Reflection;

namespace WebApp;

// The running build's version. Stamped via /p:InformationalVersion at publish time from the
// host's MinVer (see Dockerfile + CI - Image). Local builds fall back to the assembly version.
public static class AppVersion
{
    public static string Current { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}
