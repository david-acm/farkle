# Operations runbook — Azure deployment

How the Farkle app is deployed and operated on Azure Container Apps. For the
Bicep resource breakdown see [`README.md`](./README.md); for the *why* behind
the non-obvious settings see [`docs/lessons-learned.md`](../docs/lessons-learned.md).

## Topology

Two resource groups in **East US 2** (`eastus2`):

| RG | Lifecycle | Contents |
|---|---|---|
| `rg-hotdice-eus2-shared` | **persistent** (survives teardown) | ACR, Key Vault, user-assigned managed identity |
| `rg-hotdice-eus2-prod` | **disposable** (recreated each cycle) | Postgres, EventStore, Container Apps env, WebApp, budget |

> **Region note:** the stack runs in **eastus2, not eastus** — the subscription is
> offer-restricted for PostgreSQL Flexible Server in `eastus` (`LocationIsOfferRestricted`).
> `AZURE_LOCATION` and the `rg-hotdice-eus2-*` names reflect this.

## Pipelines (GitHub Actions, OIDC — no stored cloud creds)

| Workflow | Role | Trigger |
|---|---|---|
| `infra-persistent.yml` — *Infra Platform (persistent)* | Deploy ACR/KV/identity | `infra/persistent.*` change, manual |
| `build-image.yml` — *CI · Image* | Build WebApp image → push to ACR → **chain** workload CD | push to `main` (`src/**`), manual |
| `infra-deploy.yml` — *Infra Deploy (workload)* | Deploy the disposable workload (reusable) | chained from CI, `infra/main.bicep`/`workload.bicep`/`*.bicepparam` push, manual, `workflow_call` |
| `infra-validate.yml` | PR-time Bicep build+lint+PSRule (credential-free) | PR touching `infra/**` |
| `infra.yml` | `what-if` preview | manual |
| `infra-teardown.yml` | `az group delete` the workload RG | manual, `workflow_call` |
| `infra-schedule.yml` / `infra-cost-guard.yml` | Cron provision/teardown + budget guard | hourly |

**Build-once / deploy-many:** CI builds one image and chains the workload deploy in the
**same run** (`needs:` + `workflow_call`, not `workflow_run`). App CI (`CI`/storyboard)
skips infra/docs-only PRs via a `paths` filter.

## Deploying

1. Merge to `main` (or `gh workflow run infra-deploy.yml --ref main -f image_tag=latest`).
2. **Approve the gate.** The `production` environment has a **required reviewer** (`david-acm`),
   so every deploy job waits. The terminal `!` prompt truncates multi-word commands and the
   GitHub iOS app doesn't reliably show deployment approvals — approve via API:
   ```bash
   RUN=<run-id>
   EID=$(gh api repos/david-acm/farkle/actions/runs/$RUN/pending_deployments --jq '.[0].environment.id')
   gh api repos/david-acm/farkle/actions/runs/$RUN/pending_deployments \
     -X POST -F "environment_ids[]=$EID" -f state=approved -f comment="deploy"
   ```
   > This gate also stalls the unattended `infra-schedule`/`cost-guard` cron jobs — drop the
   > reviewer if you want those to run automatically.

## Required configuration

**Repository variables:** `DEPLOY_ENABLED=true`, `LIFECYCLE_ENABLED`, `AZURE_LOCATION=eastus`
*(note: the bicepparam reads this; the persistent stack RG name is set separately)*,
`AZURE_RESOURCE_GROUP=rg-hotdice-eus2-prod`, `AZURE_PERSISTENT_RESOURCE_GROUP=rg-hotdice-eus2-shared`,
`AZURE_BUDGET_NAME=hotdice-budget-dev`, `PROVISION_HOUR_UTC`/`TEARDOWN_HOUR_UTC`/`ACTIVE_WEEKDAYS`.

**`production` environment:** vars `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID`;
secrets `JWT_SECRET`, `PG_ADMIN_PASSWORD` (keep it **alphanumeric** — special chars break the
unquoted Npgsql connection string).

## Bootstrap order (fresh)

1. Deploy the **persistent** stack (`infra-persistent.yml`) → creates ACR/KV/identity.
2. CI builds the image → pushes to that ACR.
3. Deploy the **workload** (`infra-deploy.yml`) → resolves ACR/KV/identity from the persistent RG.

## Health & verification

```bash
BASE=https://<webapp-fqdn>
curl -s -o /dev/null -w '%{http_code}\n' $BASE/health/live    # 200
curl -s $BASE/health/ready                                    # "Healthy" (Postgres + EventStore)
curl -s -X POST $BASE/api/games -H 'Content-Type: application/json' -d '{"id":123}'  # {"id":...} 200
```

- Health probes are **TCP** (port 8080), not HTTP — `/health/*` is auth-gated and would 403 HTTP probes.
- **Container App secret changes don't roll a revision by themselves** — the WebApp sets a
  per-deploy `revisionSuffix` (off `utcNow()`) so each deploy rolls a fresh revision that re-reads secrets.

## Auth model

`Auth__RequireAuthorization=false` → **anonymous play** (the domain identifies players by name;
no account needed). Set `true` to require a JWT for game actions.

**Database auth is password-based.** Managed-identity (Entra) auth is implemented but **parked**
(`WebApp.IdentityDataSource` is dual-mode; the token call hung at startup). To revive it:
flip Postgres `authConfig` to Entra-only + drop the password from the connection-string secret;
the identity params and `AZURE_CLIENT_ID` env are still wired.

## Teardown / cleanup

- Workload teardown: `infra-teardown.yml` (deletes `rg-hotdice-eus2-prod` — **destructive**,
  includes Postgres + EventStore data). The persistent RG is untouched.
- Deleting an RG soft-deletes its Key Vault; purge before re-creating the same name:
  `az keyvault purge --name <kv> --location eastus2`.
