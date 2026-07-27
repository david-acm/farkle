using System.Runtime.CompilerServices;

namespace HotDice.WebTests;

internal static class TestEnvironment
{
    // The app's Auth:JwtSecret no longer ships in appsettings.Development.json
    // (issue #27). The integration tests register + log in and call authorized
    // endpoints with the returned JWT, so a signing key must be present both when
    // the host builds the JWT validation parameters and when LoginEndpoint mints
    // the token. Supply a throwaway key (>= 32 chars for HS256) via an environment
    // variable before any test host is built. This is a test-only value, not a secret.
    [ModuleInitializer]
    internal static void Init() =>
        Environment.SetEnvironmentVariable(
            "Auth__JwtSecret", "farkle-integration-test-signing-key-0123456789");
}
