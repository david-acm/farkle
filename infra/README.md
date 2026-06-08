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
| Cost budget | `consumption/budget/rg-scope` | monthly RG budget; emails at the configured thresholds |

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
3. Create a GitHub **environment** `production`. **Do not add required reviewers if you want the
   scheduled lifecycle automation to provision unattended** — an approval gate would stall the
   cron-triggered deploy. (Add reviewers only if you accept manual approval on every deploy.)
4. **Repository** **variables** (Settings → Secrets and variables → Actions → Variables):
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`,
   `ACR_NAME`, `AZURE_LOCATION`.
   > ⚠️ These must be **repository** (or org) variables, **not** environment variables. The jobs
   > are gated by `if: vars.AZURE_CLIENT_ID != ''`, which is evaluated *before* the job enters the
   > `production` environment — environment-scoped variables aren't visible there, so an env-only
   > `AZURE_CLIENT_ID` makes every job silently **skip**. (Secrets in step 5 may be env-scoped;
   > they're only read inside steps.)

   With a federated credential there is
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

## Cost control & lifecycle automation

To keep the dev environment cheap, the Bicep provisions a **monthly RG budget** that emails
the configured recipients at its thresholds, and two GitHub Actions workflows enforce a
spending/lifecycle policy. Both **no-op until** `vars.AZURE_CLIENT_ID` is set **and**
`vars.LIFECYCLE_ENABLED == 'true'`.

| Workflow | Schedule | Does |
|---|---|---|
| `infra-schedule.yml` | hourly | provisions at `PROVISION_HOUR_UTC` (on active weekdays), tears down at `TEARDOWN_HOUR_UTC` |
| `infra-cost-guard.yml` | hourly | reads the budget's current spend; tears down if spend ≥ the budget amount |

**Why a cost-guard instead of a budget→webhook?** Azure budget action-group webhooks can't
authenticate to the GitHub API, so the on-overspend teardown *pulls* the budget's current
spend via OIDC rather than receiving a push. The Azure budget still sends its threshold
**emails** — that's the alert.

**Teardown deletes the whole resource group** (`az group delete`), including ACR + the pushed
image and **all Postgres/EventStore data**. That's why "provision" is the full build+push+deploy
(the morning run rebuilds the image into the fresh ACR). Acceptable for a dev environment;
do not point this at anything whose data you need to keep. Teardown is idempotent (a no-op if
the RG is already gone).

### Lifecycle variables (all UTC; set as repository or environment variables)

| Variable | Example | Meaning |
|---|---|---|
| `LIFECYCLE_ENABLED` | `true` | master switch for both workflows |
| `PROVISION_HOUR_UTC` | `7` | hour to build + deploy |
| `TEARDOWN_HOUR_UTC` | `19` | hour to delete the RG |
| `ACTIVE_WEEKDAYS` | `1-5` | days provisioning runs — range `1-5` or list `1,2,3` (1=Mon…7=Sun); default `1-7` |
| `AZURE_BUDGET_NAME` | `farkle-budget-dev` | budget the cost-guard reads (defaults to `farkle-budget-dev`) |

### Budget parameters (Bicep)

`monthlyBudgetAmount` (dev `50`, prod `200`), `budgetThresholds` (`[80, 100]`), and
`budgetAlertEmails` are set in `infra/env/*.bicepparam`. **Override `budgetAlertEmails`** with a
real recipient — the default `changeme@example.com` is a placeholder (or set the
`BUDGET_ALERT_EMAIL` env var at deploy time). The cost-guard's OIDC identity also needs
**Cost Management Reader** to read current spend.
