# 9. Port the reference app's mobile crosscutting practices — translation decisions

Status: **Accepted** (#335, epic #334).

## Context

HotDice (the mobile app for HotDice, [ADR 0008](0008-mobile-via-maui-blazor-hybrid.md): MAUI
Blazor Hybrid over a shared RCL) starts from zero mobile infrastructure, but the **reference app** — a private .NET MAUI iOS+Android app the same developer ships through Azure DevOps to TestFlight
and Google Play — already paid for the mobile-specific crosscutting practices: testable-core
architecture, tiered testing with a new-code coverage gate, device build/release pipelines, an
on-device evidence loop, store runbooks, and agent tooling. Epic #334 ports them; the per-practice
verdicts live in [`../mobile-practices-inventory.md`](../mobile-practices-inventory.md).

Several practices cannot cross as-is — different CI system, different tracker, a different UI
model, and a repo philosophy (ADR 0004/0005: *embrace, don't abstract*) that the reference app's seam-heavy
testability pattern appears to contradict. This ADR pins the translations so the follow-up steps
(#336–#342) don't re-litigate them.

## Decisions

1. **Port the practice, translate the mechanism.** The unit of reuse is the decision (e.g. "the
   PR gate must run with no device, emulator, or MAUI workload"), not the YAML. Nothing is
   copy-pasted from ADO syntax; every ported artifact is re-expressed in this repo's stack and
   proven by demonstrating its own gate firing.

2. **Tracker: GitHub issues + sub-issues; backlog-as-code is dropped.** The reference app keeps
   `backlog/items.yaml` as source of truth and pushes to Azure DevOps with an idempotent
   importer. Here, issues (with native sub-issues under epic #334) *are* the source of truth,
   `Closes #<id>` stays the linking rule, and no importer exists or is wanted. The epic/sub-issue
   structure replaces the reference app's `us_<id>` work-item machinery.

3. **CI translation shape.** ADO stage templates + `parameters` → **reusable workflows**
   (`workflow_call` + typed inputs); variable groups → Actions/environment **secrets**; secure
   files → **base64 secrets** decoded at build (temp keychain for iOS identity); ADO
   environments/approvals → GitHub **environments with required reviewers** (the infra
   `production` gate is the in-repo precedent); pipeline-resource CD trigger → `workflow_run` +
   artifact download; `schedules:` → `on: schedule`. The reference app's cost model is kept: fast Linux gate
   on every PR → Android per merge → iOS post-merge on macOS → device-UI nightly.

4. **Seams are argued per-seam, not imported as a pattern.** The reference app's `Core`-library seams exist
   to keep MAUI types out of unit tests — a mechanical payoff, the same reason `HotDice.Shared`
   exists for the WASM client (ADR 0006). That justification survives ADR 0004/0005; dogmatic
   ports-for-purity do not. Concretely: the **shared RCL is the testable core** (bUnit), the MAUI
   shell stays thin, and a shell seam (secure storage, connectivity, lifecycle) is admitted only
   when it buys off-device testability. Verdicts land in #336's ADR.

5. **The mobile client consumes the existing API surface.** No hand-rolled typed clients: the app
   uses the Kiota **`HotDice.ApiClient`** and **`HotDice.Shared`**, so the `verify-generated` drift
   discipline automatically covers the mobile client. New standalone-client plumbing (absolute
   backend URL, CORS, token auth) follows `docs/mobile-strategy.md`; the reference app's 401-refresh
   `DelegatingHandler` pattern is adopted if/when refresh tokens exist.

6. **SignalR mobile lifecycle is net-new work, sized as such.** The reference app has no real-time practice to
   port. Foreground connect / background disconnect / `WithAutomaticReconnect` / resume-time
   re-sync get a testable handler seam in #336 and device coverage in #339's smoke.

7. **Deployments are validated, not assumed.** A green build is not a working deployment. The web/API
   side already does this — `app-release.yml`'s `post-deploy-e2e` job (#231, hardened in #235) polls
   `/health/ready`, drives the Playwright happy path against the live URL via `E2E_BASE_URL`, and emits
   a scoped App Insights link, report-only. Mobile gets the same treatment in #345 (happy path against
   the deployed backend — the only place CORS, token auth without a same-origin cookie, and SignalR over
   a real network are exercised — plus release-channel checks that a promoted build is available and
   launches). Neither smoke auto-rolls back; a store build cannot be unshipped, so halting is a human call.

8. **The evidence loop extends `storyboard.yml`, not a parallel system.** The reference app's
   screenshots+video-on-every-user-facing-PR convention is adopted for mobile, delivered through
   the same PR-artifact/comment pattern the storyboard job already established. The device smoke
   tier is deliberately **smaller** than the reference app's, because bUnit + Playwright + storyboard already
   cover the shared RCL; the device tier proves only shell-specific risk (cold launch/blank
   WebView, auth, one action, one SignalR push).

9. **Conventions merge into CLAUDE.md — no forked conventions doc.** The reference app's deltas (agent
   autonomy boundaries, test-first-on-touched-code, determinism hygiene, evidence convention)
   are folded into the existing CLAUDE.md standards (TDD red/green, 5-commit limit,
   generated-files policy, `Closes #N`). Where the two disagree, the existing HotDice rule wins
   unless the step's PR argues otherwise.

## Consequences

- Steps #336–#342 have their translation questions answered up front; each still records its
  own narrower decisions (seam verdicts, UI-driver spike) where the inventory flags them.
- Dropped practices (MVVM toolkit stack, MFA-bypass account, managed-private distribution,
  backlog importer, ADO-specific CI lore) are recorded with reasons in the inventory —
  deliberate, revisitable choices rather than omissions.
- The reference app repo remains the reference implementation; the inventory cites its paths so each step
  can consult the original before re-expressing it.
