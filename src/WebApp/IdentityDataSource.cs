using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace WebApp;

/// <summary>
/// Builds the Npgsql data source for the Identity database.
///
/// If the connection string already carries a <c>Password</c> (local dev and the
/// Testcontainers-backed integration tests) it is used as-is — plain password auth.
/// Otherwise (Azure) there is no password, and we authenticate with an Entra access
/// token obtained from the WebApp's managed identity, refreshed automatically before
/// it expires. The <c>Username</c> in the connection string must be the managed
/// identity's name, which Postgres maps to its Entra administrator role.
/// </summary>
public static class IdentityDataSource
{
    // Resource for which Postgres Flexible Server accepts an Entra access token.
    private static readonly string[] PostgresAadScope = ["https://ossrdbms-aad.database.windows.net/.default"];

    public static NpgsqlDataSource Build(string? connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);

        if (string.IsNullOrEmpty(builder.ConnectionStringBuilder.Password))
        {
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
        }

        return builder.Build();
    }
}
