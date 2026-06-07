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
workflow (issue #22).

> **Post-deploy note:** the app's public origin (`BackendUrl` / `Cors:AllowedOrigins`)
> depends on the assigned Container App FQDN (output `webAppFqdn`); set those once the
> FQDN is known if cross-origin/WASM behavior needs it.
