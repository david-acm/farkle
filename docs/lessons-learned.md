# Lessons learned

Hard-won lessons from building and debugging the Azure deployment. Most are
generic enough to apply beyond this repo. Format: **symptom → cause → lesson**.

## Azure Verified Modules (AVM) default to locked-down

The single biggest recurring theme. AVM modules are "secure by default" — they
disable network access and password auth unless you explicitly enable them. A
resource that *deploys successfully* can still be **unreachable or unusable**.

| Symptom | Cause | Fix |
|---|---|---|
| Postgres deploy: `name property cannot be null` | unrelated (empty RG var) — but the AVM Postgres module also defaulted `publicNetworkAccess: Disabled` | set `publicNetworkAccess: 'Enabled'` |
| `28P01 password authentication failed` for any password | `authConfig.passwordAuth: Disabled` (Entra-only default) | set `authConfig.passwordAuth: 'Enabled'` |
| `HANotSupportedForBurstableSku` | module defaults HA to zone-redundant | `highAvailability: 'Disabled'` on Burstable |
| ESDB `MountVolume.SetUp failed … mount error(13): Permission denied` | storage `networkAcls.defaultAction: Deny` blocks the Container Apps env from mounting Azure Files | `networkAcls.defaultAction: 'Allow'` |
| App 403 `public network access on this managed environment is disabled` | managed-environment `publicNetworkAccess: Disabled` (no VNet) | `publicNetworkAccess: 'Enabled'` |

**Lesson:** when an Azure resource is created but "can't be reached / permission
denied / auth always fails," suspect the AVM secure default *first*. Set network
and auth posture **explicitly** in Bicep rather than relying on defaults — they
also change between module versions.

## Region & SKU availability is per-subscription

- **Symptom:** Postgres `LocationIsOfferRestricted` in `eastus`; fine in `eastus2`.
- **Cause:** the subscription is capacity/offer-restricted for that resource in that region.
- **Lesson:** `az ... list-skus -l <region>` shows the **catalog**, not your subscription's
  capacity — it can't predict an offer restriction. Only a deploy attempt confirms it. If a
  resource won't provision in one region, move it; everything else here runs in eastus, but
  Postgres forced the whole stack to eastus2.

## Azure Container Apps gotchas

- **Secret value changes don't roll a new revision.** The revision template references secrets
  by *name*, not value, so updating a secret leaves running replicas on the old value. Use a
  per-deploy `revisionSuffix` (we derive it from `utcNow()`) so each deploy rolls a fresh
  revision that re-reads secrets. A revision *restart* does **not** reliably re-pull secrets.
- **HTTP health probes fail when the endpoint is auth-gated.** `/health/*` returned 403 behind
  auth, so HTTP liveness/readiness probes failed and the revision stuck "Activating" even though
  the app was healthy. **TCP probes** (port-open) sidestep this; or make `/health/*` truly anonymous.
- **`DefaultAzureCredential` can hang inside a container.** It walks a chain of credential
  providers and stalls on the later ones (CLI/PowerShell) when managed identity doesn't return
  cleanly. Prefer `ManagedIdentityCredential` directly with the user-assigned client id.
- **Internal TCP services** (EventStore) won't serve until their **volume mount** succeeds — and
  the mount depends on the *storage account's* firewall, not the container.

## Connection strings & DB auth

- **`28P01 password authentication failed` is ambiguous.** It can mean a wrong password *or*
  that **password auth is disabled** on the server (Entra-only). Check `authConfig` before
  chasing the password.
- **Special characters break unquoted connection strings.** A `;` or `=` in a generated password
  truncates `Password=...;` and yields a wrong (hence rejected) password. Keep DB passwords
  **alphanumeric**, or quote the value (but Npgsql's quote-stripping is inconsistent — alphanumeric
  is simpler and reliable).
- **EF Core `Migrate()` at startup runs before Kestrel listens.** If the DB call hangs, the app
  never starts listening, so liveness probes can cycle the container — keep that connection fast
  and the liveness `initialDelay` generous.

## Process & workflow

- **Stacked PRs must merge into the right base.** After a parent PR merges to `main`, **retarget
  its children to `main` before merging them** — otherwise they merge into now-dead branches and
  their changes never reach `main` (we lost two PRs this way and had to re-land them).
- **GitHub secrets resolve at job start.** A secret rotated within seconds of triggering a run can
  be *stale* for that run — re-dispatch after the secret settles.
- **The `production` environment gate** requires manual approval; approve via
  `gh api .../pending_deployments` when the UI/mobile app won't (see the ops runbook).

## Debugging methodology

- **Read the *deepest* error.** ARM wraps failures in generic `DeploymentFailed` /
  `ResourceDeploymentFailure`. Drill into nested operations
  (`az deployment operation group list -g <rg> --name <module>`) to get the real cause.
- **Container *system* events are gold for silent hangs.** The ESDB `MountVolume.SetUp failed`
  events (not the app console logs) pinpointed the storage-firewall root cause. Check
  `az containerapp logs --type system` and the revision `healthState`/`runningState`.
- **Prefer a definitive test over endless reasoning.** When the cause is ambiguous, change *one*
  variable and observe (e.g. swapping to TCP probes both fixed *and* proved the app was listening).
  We burned cycles theorizing about a "token hang" that the probe change would have isolated sooner.
- **Distinguish 403 vs 503 vs hang.** 403 = something is serving but forbidding; 503 = no healthy
  backend; a silent stall with no error = a blocking call (DB/token) that never returns. Each
  points at a different layer.
