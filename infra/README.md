# Infrastructure (Azure Bicep)

Repeatable Infrastructure-as-Code for deploying HotDice to **Azure Container Apps**,
using [Azure Verified Modules](https://aka.ms/avm). Replaces the old manual
PowerShell + Container App YAML that used to live under `src/Scripts/`.

## What it provisions

The infrastructure is split into **two stacks** in **two resource groups** so the
cost-saving nightly teardown only destroys disposable compute/data:

**Persistent stack** — `persistent.bicep` (subscription scope) → `modules/persistent.bicep`.
A resource group the teardown **never** touches:

| Resource | AVM module | Notes |
|---|---|---|
| Container Registry | `container-registry/registry` | private; holds the WebApp image (survives teardown) |
| Key Vault | `key-vault/vault` | RBAC; holds the static `jwt-secret` (never destroyed → no soft-delete churn) |
| User-assigned managed identity | `managed-identity/user-assigned-identity` | stable `AcrPull` + `Key Vault Secrets User` |

**Disposable stack** — `main.bicep` (subscription scope) → `modules/workload.bicep`.
Created/destroyed each lifecycle cycle; references the persistent stack by output:

| Resource | AVM module | Notes |
|---|---|---|
| Log Analytics workspace | `operational-insights/workspace` | Container Apps logs |
| PostgreSQL Flexible Server | `db-for-postgre-sql/flexible-server` | the single stateful store (ADR 0004): Marten's event store + Identity, DB `farkle_identity` |
| Container Apps environment | `app/managed-environment` | wired to Log Analytics |
| WebApp container app | `app/container-app` | external `:8080`, pulls **privately** from the persistent ACR via the persistent identity; health probes |
| Cost budget | `consumption/budget/rg-scope` | monthly RG budget; emails at the configured thresholds |

**Secrets** never live in source. `jwt-secret` lives in the persistent Key Vault and the
WebApp reads it via a KV reference using the persistent managed identity (`Auth__JwtSecret`).
The Postgres connection string (`ConnectionStrings__Identity`) is assembled as a Container App
**value secret** from the disposable Postgres FQDN + the `@secure()` admin password. Marten shares
that same Postgres (its own schema), so there is no separate event-store connection string.

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

Two stages — the persistent stack once, then the disposable workload per cycle.

```bash
# 1) persistent stack (ACR + KV + identity) — once
export JWT_SECRET='<>= 32-char signing key>'
az deployment sub create \
  --location eastus \
  --template-file infra/persistent.bicep \
  --parameters infra/env/persistent.bicepparam

# read its outputs for the disposable deploy
export ACR_LOGIN_SERVER=$(az acr list -g hotdice-shared-rg --query '[0].loginServer' -o tsv)
export KEY_VAULT_URI=$(az keyvault list -g hotdice-shared-rg --query '[0].properties.vaultUri' -o tsv)
export IDENTITY_RESOURCE_ID=$(az identity list -g hotdice-shared-rg --query '[0].id' -o tsv)

# 2) disposable workload — per cycle (image must already be in the persistent ACR)
export PG_ADMIN_PASSWORD='<strong-password>'
export IMAGE_TAG='<git-sha-or-latest>'
az deployment sub create \
  --location eastus \
  --template-file infra/main.bicep \
  --parameters infra/env/dev.bicepparam   # use what-if to preview
```

Deploy the **persistent stack once** first (`infra/persistent.bicep`); the disposable deploy
reads its ACR login server / Key Vault URI / identity id (the CD workflow resolves these
automatically). The WebApp image must already be in the persistent ACR (CI pushes it).

## CI/CD pipelines (GitHub Actions + OIDC)

Three concerns, three pipelines:

| Pipeline | Workflow | Trigger | Does |
|---|---|---|---|
| **CI** | `build-image.yml` | push to `main` (`src/**`), manual | build the WebApp image, push `webapp:<sha>` + `:latest` to the **persistent ACR**, then chain CD (via `workflow_call`) to deploy that exact `<sha>` |
| **CD (workload)** | `infra-deploy.yml` | push to `main` (`infra/**`), manual, `workflow_call` | resolve the persistent stack refs and `az deployment sub create` the disposable workload for a given `image_tag` (default `latest`) |
| **Platform** | `infra-persistent.yml` | push to `main` (persistent templates), manual | deploy the persistent ACR/KV/identity |
| | `infra-teardown.yml` | manual, `workflow_call` | `az group delete` on the **disposable** RG (persistent untouched) |
| | `infra.yml` | manual | `az deployment sub what-if` → run summary |

CI chains CD with **`workflow_call`/`needs`** (not `workflow_run`), so the deploy runs in the
same logical run, shares context, and ships the **exact** `<sha>` that was built.

All use **OIDC** (no stored cloud credentials). They **no-op until enabled**: deploy/teardown/CI
are gated on the repository variable `DEPLOY_ENABLED == 'true'`, the lifecycle workflows on
`LIFECYCLE_ENABLED == 'true'`. PR-time Bicep checking is done credential-free by
`infra-validate.yml`, so the what-if is a manual preview rather than a PR check.

### Variable scoping (important)

GitHub evaluates a job-level `if:` **before** the job enters its `environment:`, and a job with
no environment can't read environment-scoped variables at all. So the config splits in two:

- **`production` environment** — the Azure target config + secrets (read inside steps):
  `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`
  (disposable RG), `AZURE_PERSISTENT_RESOURCE_GROUP`, `AZURE_LOCATION`, and secrets `JWT_SECRET`,
  `PG_ADMIN_PASSWORD`. (The ACR + Key Vault names are derived; the workflows resolve them from the
  persistent RG — no `ACR_NAME` needed.)
- **Repository variables** — the non-sensitive switches/timing used in `if:`/gate steps:
  `DEPLOY_ENABLED`, `LIFECYCLE_ENABLED`, `PROVISION_HOUR_UTC`, `TEARDOWN_HOUR_UTC`,
  `ACTIVE_WEEKDAYS`, `AZURE_BUDGET_NAME`.

> Putting the gating flags (`DEPLOY_ENABLED` / `LIFECYCLE_ENABLED`) in the environment instead of
> the repository makes every job silently **skip** — that's the one thing that must be repo-scoped.

### One-time setup (Azure + GitHub)
1. Create an Entra app registration and add a **federated credential** for GitHub OIDC:
   - subject `repo:david-acm/farkle:environment:production` (all Azure jobs enter the `production` environment)
   - issuer `https://token.actions.githubusercontent.com`, audience `api://AzureADTokenExchange`
2. Grant it rights on the target subscription. The **persistent** stack creates role assignments
   (it grants the managed identity **Key Vault Secrets User** `4633458b-17de-408a-b874-0445c86b69e6`
   on the vault and **AcrPull** `7f951dda-4ed3-4680-a7ca-43fe172d538d` on the registry), and **CI
   needs AcrPush** to push images, so Contributor alone isn't enough. Either:
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
3. Create a GitHub **environment** `production`. Because the scheduled lifecycle automation and
   the cost-guard enter this environment unattended, **do not add required reviewers** (an approval
   gate would stall the cron-triggered deploy/teardown) and leave **deployment branches**
   unrestricted enough for the workflows that use it. Add reviewers only if you accept manual
   approval on every automated action.
4. In the **`production` environment**, add the Azure target config as **environment variables**:
   `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_RESOURCE_GROUP`
   (disposable), `AZURE_PERSISTENT_RESOURCE_GROUP` (e.g. `hotdice-shared-rg`), `AZURE_LOCATION`;
   and **environment secrets** `JWT_SECRET`, `PG_ADMIN_PASSWORD`
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
   repository-scoped (see *Variable scoping* above). The disposable RG name is driven by
   `AZURE_RESOURCE_GROUP` so deploy and teardown always agree.
6. **Deploy the persistent stack once** (Actions → *Infra Platform (persistent)* → Run workflow, or
   `az deployment sub create --template-file infra/persistent.bicep --parameters infra/env/persistent.bicepparam`).
   CI/CD resolve the ACR/KV/identity from `AZURE_PERSISTENT_RESOURCE_GROUP`, so this must exist
   before the first image build/deploy.

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

**Teardown deletes the whole *disposable* resource group** (`az group delete`), including
**all Postgres data** (Marten's events + Identity). The **persistent** RG (ACR + Key Vault + identity) is untouched,
so the image survives and "provision" just **redeploys the latest image** — no rebuild. Acceptable
for a dev environment; do not point this at anything whose data you need to keep. Teardown is
idempotent (a no-op if the RG is already gone).

### Lifecycle variables (UTC; **repository** variables — see *Variable scoping*)

| Variable | Example | Meaning |
|---|---|---|
| `LIFECYCLE_ENABLED` | `true` | master switch for both workflows |
| `PROVISION_HOUR_UTC` | `7` | hour to build + deploy |
| `TEARDOWN_HOUR_UTC` | `19` | hour to delete the RG |
| `ACTIVE_WEEKDAYS` | `1-5` | days provisioning runs — range `1-5` or list `1,2,3` (1=Mon…7=Sun); default `1-7` |
| `AZURE_BUDGET_NAME` | `hotdice-budget-dev` | budget the cost-guard reads (defaults to `hotdice-budget-dev`) |

### Budget parameters (Bicep)

`monthlyBudgetAmount` (dev `50`, prod `200`), `budgetThresholds` (`[80, 100]`), and
`budgetAlertEmails` are set in `infra/env/*.bicepparam`. **Override `budgetAlertEmails`** with a
real recipient — the default `changeme@example.com` is a placeholder (or set the
`BUDGET_ALERT_EMAIL` env var at deploy time). The cost-guard's OIDC identity also needs
**Cost Management Reader** to read current spend.
