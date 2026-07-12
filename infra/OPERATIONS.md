# Operations runbook — Azure deployment

How the Farkle app is deployed and operated on Azure Container Apps. For the
Bicep resource breakdown see [`README.md`](./README.md); for the *why* behind
the non-obvious settings see [`docs/lessons-learned.md`](../docs/lessons-learned.md).

## Topology

Two resource groups in **East US 2** (`eastus2`):

| RG | Lifecycle | Contents |
|---|---|---|
| `rg-hotdice-eus2-shared` | **persistent** (survives teardown) | ACR, Key Vault, user-assigned managed identity |
| `rg-hotdice-eus2-prod` | **disposable** (recreated each cycle) | Postgres (Marten events + Identity), Container Apps env, WebApp, budget |

> **Region note:** the stack runs in **eastus2, not eastus** — the subscription is
> offer-restricted for PostgreSQL Flexible Server in `eastus` (`LocationIsOfferRestricted`).
> `AZURE_LOCATION` and the `rg-hotdice-eus2-*` names reflect this.

## Pipelines (GitHub Actions, OIDC — no stored cloud creds)

| Workflow | Role | Trigger |
|---|---|---|
| `infra-persistent.yml` — *CD - Infra Platform (persistent)* | Deploy ACR/KV/identity | `infra/persistent.*` change, manual |
| `build-image.yml` — *CI - Image* | Build WebApp image → push to ACR → **chain** App Release | push to `main` (`src/**`), manual |
| `app-release.yml` — *CD - App Release* | Fast image roll (`az containerapp update`, no ARM, **ungated**) | chained from CI, manual, `workflow_call` |
| `infra-deploy.yml` — *CD - Infra Deploy (workload)* | Full-stack ARM deploy of the disposable workload (gated) | `infra/main.bicep`/`workload.bicep`/`*.bicepparam` push, scheduled re-provision, manual, `workflow_call` |
| `infra-validate.yml` — *CI - Infra Validate* | PR-time Bicep build+lint+PSRule (credential-free) | PR touching `infra/**` |
| `infra.yml` — *CI - Infra What-If* | `what-if` preview | manual |
| `infra-teardown.yml` — *Ops - Infra Teardown* | `az group delete` the workload RG | manual, `workflow_call` |
| `infra-schedule.yml` / `infra-cost-guard.yml` — *Ops - Infra Schedule* / *Ops - Infra Cost Guard* | Cron provision/teardown + budget guard | hourly |

**Two deploy paths.** Draw the line at *"resource topology/config, or just the running app version?"*:
- **App release (fast, everyday):** a `src/**` push → `CI - Image` builds/pushes the image →
  chains `CD - App Release`, which does only `az containerapp update --image …:<sha>` (seconds,
  no ARM, **no gate**). This is the common case.
- **Infra deploy (full ARM, gated):** an `infra/**` push (and the scheduled morning re-provision)
  → `CD - Infra Deploy (workload)` re-evaluates the whole workload via `az deployment sub create`.

**Build-once / deploy-many:** CI builds one image (tagged `:<sha>` + `:latest`) and chains the
release in the **same run** (`needs:` + `workflow_call`). `App Release` and `Infra Deploy` share a
`concurrency: cd-deploy` lock so they never run at once. Because both tags are pushed together and
every full-ARM deploy uses `:latest`, the running `:<sha>` and `:latest` stay in lockstep — an infra
deploy never reverts an app release.

## Deploying

**App change** (`src/**`): merge to `main` → `CI - Image` builds → `CD - App Release` rolls it
**automatically** (no approval). Roll back by re-running App Release with a prior SHA:
`gh workflow run app-release.yml --ref main -f image_tag=<sha>`. If the workload RG is currently
torn down, App Release skips with a notice — the image is in ACR and the next provision deploys it.

**Infra change** (`infra/**`) or full re-provision: triggers `CD - Infra Deploy (workload)`, which
is **gated** on the `production` environment (required reviewer `david-acm`). The terminal `!` prompt
truncates multi-word commands and the GitHub iOS app doesn't reliably show approvals — approve via API:
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
`AZURE_BUDGET_NAME=hotdice-budget-dev`, `PROVISION_HOUR_UTC`/`TEARDOWN_HOUR_UTC`/`ACTIVE_WEEKDAYS`,
and the OIDC identifiers `AZURE_CLIENT_ID`/`AZURE_TENANT_ID`/`AZURE_SUBSCRIPTION_ID` (non-secret;
repo-level so the **ungated** `CI - Image` build and `CD - App Release` can authenticate without the
`production` environment).

**`production` environment:** the required-reviewer gate (used only by `CD - Infra Deploy` /
`CD - Infra Platform`) plus secrets `JWT_SECRET`, `PG_ADMIN_PASSWORD` (keep it **alphanumeric** —
special chars break the unquoted Npgsql connection string). It also still carries env-level copies of
the `AZURE_*` OIDC ids (harmless; gated jobs resolve either scope).

## Bootstrap order (fresh)

1. Deploy the **persistent** stack (`infra-persistent.yml`) → creates ACR/KV/identity.
2. CI builds the image → pushes to that ACR.
3. Deploy the **workload** (`infra-deploy.yml`) → resolves ACR/KV/identity from the persistent RG.

## Health & verification

```bash
BASE=https://<webapp-fqdn>
curl -s -o /dev/null -w '%{http_code}\n' $BASE/health/live    # 200
curl -s $BASE/health/ready                                    # "Healthy" (Postgres — Marten events + Identity)
curl -s -X POST $BASE/api/games -H 'Content-Type: application/json' -d '{"id":123}'  # {"id":...} 200
```

- Health probes are **TCP** (port 8080), not HTTP — `/health/*` is auth-gated and would 403 HTTP probes.
- **Container App secret changes don't roll a revision by themselves** — the WebApp sets a
  per-deploy `revisionSuffix` (off `utcNow()`) so each deploy rolls a fresh revision that re-reads secrets.

## Schema & Critter Stack CLI (ADR 0004)

Postgres is the single stateful store: **Marten owns its own schema** (events + the `GameState`
snapshot) and **EF Core owns the Identity schema**. There are no hand-written migrations for the
event store — Marten reconciles its schema on startup (`AutoCreate.CreateOrUpdate`), and EF applies
its Identity migrations on startup as today.

The JasperFx command line is wired into the host (`dotnet run -- <command>`), so the same binary can
inspect or manage the stack:

```bash
dotnet run --project src/WebApp -- describe            # configuration + discovered endpoints/handlers
dotnet run --project src/WebApp -- resources list      # Marten/Wolverine resources this app owns
dotnet run --project src/WebApp -- resources setup     # apply schema explicitly (a.k.a. db-apply)
dotnet run --project src/WebApp -- projections rebuild # rebuild projections if one is added later
dotnet run --project src/WebApp -- codegen write       # regenerate the committed prod codegen (see below)
```

**Codegen (dev fast, prod static).** Development and tests generate the Wolverine handler/endpoint code
in-memory (`TypeLoadMode.Dynamic`, zero friction). **Production** (`ASPNETCORE_ENVIRONMENT=Production`)
loads committed, pre-generated code from `src/WebApp/Internal/Generated` (`TypeLoadMode.Static`) → fast
cold start, no runtime Roslyn, and it fails fast if that code is missing. After changing a handler or
endpoint, run `dotnet run --project src/WebApp -- codegen write` and commit the result; the
`verify-codegen` CI job regenerates and fails on drift (the codegen analogue of `verify-generated`).

> Projection rebuild is listed for completeness — `GameState` is an **Inline** snapshot today, so there
> is no async projection to rebuild yet.

## Auth model

`Auth__RequireAuthorization=false` → **anonymous play** (the domain identifies players by name;
no account needed). Set `true` to require a JWT for game actions.

**Database auth is password-based.** Managed-identity (Entra) auth is implemented but **parked**
(`WebApp.IdentityDataSource` is dual-mode; the token call hung at startup). To revive it:
flip Postgres `authConfig` to Entra-only + drop the password from the connection-string secret;
the identity params and `AZURE_CLIENT_ID` env are still wired.

## Teardown / cleanup

- Workload teardown: `infra-teardown.yml` (deletes `rg-hotdice-eus2-prod` — **destructive**,
  includes all Postgres data: Marten's events + Identity). The persistent RG is untouched.
- Deleting an RG soft-deletes its Key Vault; purge before re-creating the same name:
  `az keyvault purge --name <kv> --location eastus2`.
