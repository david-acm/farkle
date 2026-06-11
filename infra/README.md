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
| User-assigned managed identity | `managed-identity/user-assigned-identity` | Key Vault read |
| PostgreSQL Flexible Server | `db-for-postgre-sql/flexible-server` | managed Identity DB (`farkle_identity`) |
| Key Vault | `key-vault/vault` | RBAC; holds the JWT secret + Postgres connection string |
| Storage account + file share | `storage/storage-account` | Azure Files volume for EventStore data |
| Container Apps environment | `app/managed-environment` | wired to Log Analytics + the file share |
| EventStore container app | `app/container-app` | internal TCP `:2113`, persistent volume |
| WebApp container app | `app/container-app` | external `:8080`, image pulled anonymously from public GHCR, KV-referenced secrets, health probes |
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
export IMAGE_TAG='<git-sha-or-"latest" of the webapp image in GHCR>'

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

The WebApp image is built and published to **public GHCR** by CI
(`build-image.yml`) — independently of Azure, so the deploy only references an
image that already exists (`imageRepository`:`imageTag`). `imageRepository`
defaults to `ghcr.io/david-acm/farkle-webapp`.

## CI/CD deploy (GitHub Actions + OIDC)

The image build (CI) and the deploy (CD) are separate pipelines — **build once,
deploy many**:

| Workflow | Trigger | Does |
|---|---|---|
| `build-image.yml` (CI · Image) | push to `main` (`src/**`), manual | build + push the WebApp image to public GHCR (`:<sha>` + `:latest`) via the automatic `GITHUB_TOKEN` |
| `infra-deploy.yml` (Deploy · CD) | after CI publishes an image (`workflow_run`), push to `main` (`infra/**`), manual, or `workflow_call` | `az deployment sub create` referencing the GHCR image (`image_tag` input, default `latest`) |
| `infra-teardown.yml` | manual or `workflow_call` | `az group delete` on the workload RG (**destructive**) |
| `infra.yml` | manual | `az deployment sub what-if` → predicted changes in the run summary |

> **One-time:** after the first `build-image` run, set the `farkle-webapp` GHCR
> package visibility to **public** (GitHub → your profile/org → Packages → package
> → Package settings) so the Container App can pull it without credentials.

The deploy workflows use **OIDC** (no stored cloud credentials); the image build
needs no cloud credentials at all. They **no-op until enabled**: deploy/teardown
are gated on the repository variable `DEPLOY_ENABLED == 'true'`, the lifecycle workflows on
`LIFECYCLE_ENABLED == 'true'`. PR-time Bicep checking is done credential-free by
`infra-validate.yml`, so the what-if is a manual preview rather than a PR check.

### Variable scoping (important)

GitHub evaluates a job-level `if:` **before** the job enters its `environment:`, and a job with
no environment can't read environment-scoped variables at all. So the config splits in two:

- **`production` environment** — the Azure target config + secrets (read inside steps):
  `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`,
  `AZURE_LOCATION`, and secrets `JWT_SECRET`, `PG_ADMIN_PASSWORD`.
- **Repository variables** — the non-sensitive switches/timing used in `if:`/gate steps:
  `DEPLOY_ENABLED`, `LIFECYCLE_ENABLED`, `PROVISION_HOUR_UTC`, `TEARDOWN_HOUR_UTC`,
  `ACTIVE_WEEKDAYS`, `AZURE_BUDGET_NAME`.

> Putting the gating flags (`DEPLOY_ENABLED` / `LIFECYCLE_ENABLED`) in the environment instead of
> the repository makes every job silently **skip** — that's the one thing that must be repo-scoped.

### One-time setup (Azure + GitHub)
1. Create an Entra app registration and add a **federated credential** for GitHub OIDC:
   - subject `repo:david-acm/farkle:environment:production` (all Azure jobs enter the `production` environment)
   - issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`
2. Grant it rights on the target subscription. The Bicep both creates resources **and**
   creates a role assignment (it grants the managed identity **Key Vault Secrets User**
   `4633458b-17de-408a-b874-0445c86b69e6` on the vault), so Contributor alone isn't enough.
   Either:
   - the simple option — **Owner**; or
   - least-privilege — **Contributor** + **Role Based Access Control Administrator** with an
     ABAC condition restricting it to *only* that role ID, so it can never grant any
     other role:

     ```text
     (
      ( !(ActionMatches{'Microsoft.Authorization/roleAssignments/write'}) )
      OR
      ( @Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {4633458b-17de-408a-b874-0445c86b69e6} )
     )
     AND
     (
      ( !(ActionMatches{'Microsoft.Authorization/roleAssignments/delete'}) )
      OR
      ( @Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {4633458b-17de-408a-b874-0445c86b69e6} )
     )
     ```

     (`--condition-version "2.0"`.)

   Also grant **Cost Management Reader** (read-only; for the budget cost-guard, PR4).
3. Create a GitHub **environment** `production`. Because the scheduled lifecycle automation and
   the cost-guard enter this environment unattended, **do not add required reviewers** (an approval
   gate would stall the cron-triggered deploy/teardown) and leave **deployment branches**
   unrestricted enough for the workflows that use it. Add reviewers only if you accept manual
   approval on every automated action.
4. In the **`production` environment**, add the Azure target config as **environment variables**:
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`,
   `AZURE_LOCATION`; and **environment secrets** `JWT_SECRET`, `PG_ADMIN_PASSWORD`
   (the deploy reads the secrets via the `.bicepparam`'s `readEnvironmentVariable`). With a
   federated credential there is **no client secret** — OIDC exchanges a short-lived token, so the
   `AZURE_*` values are plain IDs. Where to find them:
   - `AZURE_CLIENT_ID` — Entra ID → App registrations → your app → **Overview** → "Application (client) ID".
   - `AZURE_TENANT_ID` — same Overview page → "Directory (tenant) ID".
   - `AZURE_SUBSCRIPTION_ID` — Subscriptions → your subscription → "Subscription ID".
5. As **repository variables** (Settings → Secrets and variables → Actions → Variables →
   *Repository variables*), add the gating switches: `DEPLOY_ENABLED=true` to turn on push/manual
   deploys + teardown, and (for the lifecycle automation) `LIFECYCLE_ENABLED=true` plus
   `PROVISION_HOUR_UTC`, `TEARDOWN_HOUR_UTC`, `ACTIVE_WEEKDAYS`, `AZURE_BUDGET_NAME`. These must be
   repository-scoped (see *Variable scoping* above). The RG name is driven by `AZURE_RESOURCE_GROUP`
   so deploy and teardown always agree.

> **Post-deploy note:** the app's public origin (`BackendUrl` / `Cors:AllowedOrigins`)
> depends on the assigned Container App FQDN (output `webAppFqdn`); set those once the
> FQDN is known if cross-origin/WASM behavior needs it.

## Cost control & lifecycle automation

To keep the dev environment cheap, the Bicep provisions a **monthly RG budget** that emails
the configured recipients at its thresholds, and two GitHub Actions workflows enforce a
spending/lifecycle policy. Both **no-op until** `vars.LIFECYCLE_ENABLED == 'true'`; the teardown
they invoke is additionally gated on `vars.DEPLOY_ENABLED == 'true'`.

| Workflow | Schedule | Does |
|---|---|---|
| `infra-schedule.yml` | hourly | provisions at `PROVISION_HOUR_UTC` (on active weekdays), tears down at `TEARDOWN_HOUR_UTC` |
| `infra-cost-guard.yml` | hourly | reads the budget's current spend; tears down if spend ≥ the budget amount |

**Why a cost-guard instead of a budget→webhook?** Azure budget action-group webhooks can't
authenticate to the GitHub API, so the on-overspend teardown *pulls* the budget's current
spend via OIDC rather than receiving a push. The Azure budget still sends its threshold
**emails** — that's the alert.

**Teardown deletes the whole resource group** (`az group delete`), including **all
Postgres/EventStore data**. The WebApp image lives in GHCR (outside the RG), so it
survives teardown — "provision" just redeploys the existing `latest` image, no
rebuild. Acceptable for a dev environment; do not point this at anything whose data
you need to keep. Teardown is idempotent (a no-op if the RG is already gone).

### Lifecycle variables (UTC; **repository** variables — see *Variable scoping*)

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
