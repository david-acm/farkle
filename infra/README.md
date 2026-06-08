# Infrastructure (Azure Bicep)

Repeatable Infrastructure-as-Code for deploying Farkle to **Azure Container Apps**,
using [Azure Verified Modules](https://aka.ms/avm). Replaces the old manual
PowerShell + Container App YAML that used to live under `src/Scripts/`.

## What it provisions

`main.bicep` (subscription scope) creates the resource group and deploys
`modules/workload.bicep`, which composes:

| Resource | AVM module | Notes |
|---|---|---|
| Log Analytics workspace | `operational-insights/workspace` | Container Apps logs |
| User-assigned managed identity | `managed-identity/user-assigned-identity` | ACR pull + Key Vault read |
| PostgreSQL Flexible Server | `db-for-postgre-sql/flexible-server` | managed Identity DB (`farkle_identity`) |
| Key Vault | `key-vault/vault` | RBAC; holds the JWT secret + Postgres connection string |
| Container Registry | `container-registry/registry` | WebApp image; identity has `AcrPull` |
| Storage account + file share | `storage/storage-account` | Azure Files volume for EventStore data |
| Container Apps environment | `app/managed-environment` | wired to Log Analytics + the file share |
| EventStore container app | `app/container-app` | internal TCP `:2113`, persistent volume |
| WebApp container app | `app/container-app` | external `:8080`, KV-referenced secrets, health probes |

**Secrets** never live in source. The JWT secret + Postgres password are passed as
`@secure()` parameters and stored in Key Vault; the WebApp reads them via the
managed identity (`Auth__JwtSecret`, `ConnectionStrings__Identity`). EventStore runs
insecure inside the environment, so its connection string is assembled in Bicep.

**Health probes** target the app's `/health/live` (liveness) and `/health/ready`
(readiness) endpoints.

## Local validation (no Azure account needed)

```bash
# az + bicep
az bicep install
az bicep build        --file infra/main.bicep          # compile + lint (errors fail)
az bicep build-params --file infra/env/dev.bicepparam

# Best-practice analysis (PSRule for Azure)
pwsh -c "Install-Module PSRule.Rules.Azure -Scope CurrentUser -Force; \
         Invoke-PSRule -InputPath ./infra/ -Module PSRule.Rules.Azure -Option ./infra/ps-rule.yaml"
```

CI runs the same build/lint + PSRule on every PR touching `infra/` (`.github/workflows/infra-validate.yml`).

## Deploy

Provide the two secrets via environment variables (or `--parameters` overrides):

```bash
export PG_ADMIN_PASSWORD='<strong-password>'
export JWT_SECRET='<>= 32-char signing key>'
export IMAGE_TAG='<git-sha-of-the-webapp-image-in-ACR>'

# preview
az deployment sub what-if \
  --location eastus \
  --template-file infra/main.bicep \
  --parameters infra/env/dev.bicepparam

# apply
az deployment sub create \
  --location eastus \
  --template-file infra/main.bicep \
  --parameters infra/env/dev.bicepparam
```

The WebApp image must be built and pushed to the registry (output
`containerRegistryLoginServer`) before/with the deploy — automated by the deploy
workflow below.

## CI/CD deploy (GitHub Actions + OIDC)

| Workflow | Trigger | Does |
|---|---|---|
| `infra.yml` | PR touching `infra/**` | `az deployment sub what-if` → posts the predicted changes as a PR comment |
| `infra-deploy.yml` | push to `main` (`infra/**`,`src/**`), manual, or `workflow_call` | build + push the WebApp image to ACR, then `az deployment sub create` |
| `infra-teardown.yml` | manual or `workflow_call` | `az group delete` on the workload RG (**destructive**) |

All use **OIDC** (no stored cloud secrets) and **no-op until configured** — every job
is gated on `vars.AZURE_CLIENT_ID`.

### One-time setup (Azure + GitHub)
1. Create an Entra app registration and add **federated credentials** for GitHub OIDC:
   - subject `repo:david-acm/farkle:environment:production`
   - subject `repo:david-acm/farkle:pull_request` (for what-if)
   - issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`
2. Grant it rights on the target subscription. The Bicep both creates resources **and**
   creates role assignments (it grants the managed identity **Key Vault Secrets User**
   `4633458b-17de-408a-b874-0445c86b69e6` on the vault and **AcrPull**
   `7f951dda-4ed3-4680-a7ca-43fe172d538d` on the registry), so Contributor alone isn't enough.
   Either:
   - the simple option — **Owner**; or
   - least-privilege — **Contributor** + **Role Based Access Control Administrator** with an
     ABAC condition restricting it to *only* those two role IDs, so it can never grant any
     other role:

     ```text
     (
      ( !(ActionMatches{'Microsoft.Authorization/roleAssignments/write'}) )
      OR
      ( @Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {4633458b-17de-408a-b874-0445c86b69e6, 7f951dda-4ed3-4680-a7ca-43fe172d538d} )
     )
     AND
     (
      ( !(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'}) )
      OR
      ( @Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {4633458b-17de-408a-b874-0445c86b69e6, 7f951dda-4ed3-4680-a7ca-43fe172d538d} )
     )
     ```

     (`--condition-version "2.0"`.)

   Also grant **Cost Management Reader** (read-only; for the budget cost-guard, PR4).
3. Create a GitHub **environment** `production` (add required reviewers if you want approval gates).
4. Repo/environment **variables**: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`,
   `AZURE_RESOURCE_GROUP`, `ACR_NAME`, `AZURE_LOCATION`. With a federated credential there is
   **no client secret** — OIDC exchanges a short-lived token, so these are plain IDs (variables,
   not secrets). Where to find them:
   - `AZURE_CLIENT_ID` — Entra ID → App registrations → your app → **Overview** → "Application (client) ID".
   - `AZURE_TENANT_ID` — same Overview page → "Directory (tenant) ID".
   - `AZURE_SUBSCRIPTION_ID` — Subscriptions → your subscription → "Subscription ID".
5. Repo/environment **secrets**: `JWT_SECRET`, `PG_ADMIN_PASSWORD` (the only long-lived secrets;
   the deploy reads them via the `.bicepparam`'s `readEnvironmentVariable`). The RG name is
   driven by the `AZURE_RESOURCE_GROUP` variable so deploy and teardown always agree.

> **Post-deploy note:** the app's public origin (`BackendUrl` / `Cors:AllowedOrigins`)
> depends on the assigned Container App FQDN (output `webAppFqdn`); set those once the
> FQDN is known if cross-origin/WASM behavior needs it.
