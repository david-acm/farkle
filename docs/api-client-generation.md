# Regenerating `swagger.json` + the Kiota API client

`src/WebApp.Client/swagger.json` and `src/Farkle.ApiClient/**` are **generated —
never hand-edit them**. Regenerate and commit after any change to the HTTP
contract (`src/Farkle.Contracts/HttpRequests.cs` / `HttpResponses.cs`). CI's
`verify-generated` job fails if the committed output differs from a fresh
regeneration.

The Kiota client is a **single shared client** consumed by both `WebApp.Client`
and `Farkle.WebTests`, so there's one regeneration command for all consumers.

## Steps

1. Edit the DTO in `src/Farkle.Contracts/HttpResponses.cs` (or `HttpRequests.cs`).
2. Regenerate `swagger.json`. Since #303 this is emitted by ASP.NET's built-in OpenAPI
   generator (`Microsoft.Extensions.ApiDescription.Server`), which **boots the host**, so a
   reachable Postgres is required (Marten + Identity). Point `ConnectionStrings:Identity` at one:
   ```bash
   ConnectionStrings__Identity="Host=localhost;Port=5432;Database=farkle_marten;Username=postgres;Password=changeit" \
     dotnet build src/WebApp/WebApp.csproj -p:GenerateSwagger=true
   ```
3. Regenerate the Kiota client (clean first so a removed path/schema can't leave an orphan):
   ```bash
   dotnet tool restore
   cd src/Farkle.ApiClient && rm -rf Api Models && dotnet kiota generate \
     -l CSharp -d ../WebApp.Client/swagger.json \
     -c FarkleApiClient -n Farkle.ApiClient -o . \
     && cd -
   ```
4. Commit the regenerated `swagger.json` + `Farkle.ApiClient/`.

> Kiota is pinned to `1.31.1` in `.config/dotnet-tools.json`. The tooling needs
> the **.NET 8 SDK** in addition to .NET 10 — in restricted sandboxes install
> both per [`remote-sessions.md`](remote-sessions.md).
