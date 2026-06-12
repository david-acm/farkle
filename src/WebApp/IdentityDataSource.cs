using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace WebApp;

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

        // DefaultAzureCredential picks the user-assigned managed identity when the
        // container sets AZURE_CLIENT_ID to that identity's client id.
        var credential = new DefaultAzureCredential();
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
