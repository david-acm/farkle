using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace Farkle.Infrastructure.Identity;

/// <summary>
/// Builds an Npgsql data source that authenticates to Postgres with an Entra access
/// token from the WebApp's managed identity, refreshed automatically before it expires.
///
/// Only used in Azure, where the connection string has a <c>Username</c> (the managed
/// identity name) and no password. Local dev and the Testcontainers-backed integration
/// tests supply a password in the connection string and keep plain password auth, so
/// they never call this (see Program.cs).
/// </summary>
public static class IdentityDataSource
{
    // Resource for which Postgres Flexible Server accepts an Entra access token.
    private static readonly string[] PostgresAadScope = ["https://ossrdbms-aad.database.windows.net/.default"];

    public static NpgsqlDataSource BuildEntra(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // Use ManagedIdentityCredential directly (not DefaultAzureCredential): we know
        // this runs as a user-assigned managed identity, so go straight to the identity
        // endpoint. DefaultAzureCredential would walk a chain and can hang on the later
        // CLI/PowerShell providers (which time out) inside a container.
        var clientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
        var managedIdentityId = string.IsNullOrEmpty(clientId)
            ? ManagedIdentityId.SystemAssigned
            : ManagedIdentityId.FromUserAssignedClientId(clientId);
        TokenCredential credential = new ManagedIdentityCredential(managedIdentityId);
        builder.UsePeriodicPasswordProvider(
            async (_, ct) =>
            {
                var token = await credential.GetTokenAsync(new TokenRequestContext(PostgresAadScope), ct);
                return token.Token;
            },
            // Refresh well before the ~60-minute token lifetime; short retry on failure.
            successRefreshInterval: TimeSpan.FromMinutes(50),
            failureRefreshInterval: TimeSpan.FromSeconds(5));

        return builder.Build();
    }
}
