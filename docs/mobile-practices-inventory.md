# Mobile practices inventory: what HotDice ports from the reference MAUI app

Deliverable of #335 (step 1/8 of epic #334). One row per crosscutting practice in the
reference app repo (a private .NET MAUI iOS+Android app by the same developer,
shipped through Azure DevOps to TestFlight and Google Play), with a **port / adapt / drop**
verdict for this repo. The cross-cutting decisions the verdicts rest on are recorded in
[ADR 0009](decisions/0009-mobile-practice-port-for-hotdice.md); the mobile stack itself is
**MAUI Blazor Hybrid over a shared RCL** ([ADR 0008](decisions/0008-mobile-via-maui-blazor-hybrid.md),
reaffirmed for the epic).

**Verdict legend:** **Port** — practice and mechanism carry over (translated ADO→GitHub where
needed). **Adapt** — the practice carries over but the mechanism is reshaped for Hybrid /
this repo. **Drop** — not carried; reason given. *Owner* is the epic step issue.

## Architecture & testing

| Practice | Reference artifact | Verdict | Target here | Owner |
|---|---|---|---|---|
| Testable core: UI-framework-free library so unit tests run off-device | its platform-free `Core` library (net10, MAUI-free; ViewModels/Services/Handlers/Models) | **Adapt** | The **shared RCL** is the testable core (bUnit), continuing `Blazor.Dice.Tests`/`Farkle.SpaTests`; only the thin MAUI shell is device-bound | #336 |
| Platform seams behind interfaces (`INavigationService`, `ISecureStorageService`, `IConnectivityService`, `IDialogService`, `IMainThreadDispatcher`) | the `Core` library + `MauiProgram.cs` DI | **Adapt** | Argued **per-seam** against the embrace-don't-abstract stance (ADR 0004/0005): a seam survives only for a mechanical payoff (off-device testability). Expected survivors: secure token storage, connectivity, app lifecycle | #336 |
| MVVM via `[ObservableProperty]`/`[RelayCommand]` + FluentValidation per-VM | Core ViewModels + `*Validator.cs` | **Drop** | Hybrid UI is Razor + BlazorState, already the repo standard; MVVM toolkit has no role in a `BlazorWebView` UI. (FluentValidation itself is already adopted server-side, #331) | — |
| Typed per-service `HttpClient`s + `RefreshHandler` (transparent JWT refresh on 401) | `Core/Handlers/RefreshHandler.cs`, named clients | **Adapt** | Consume the existing Kiota **`Farkle.ApiClient`** + **`Farkle.Shared`** (ADR 0006) — no hand-rolled clients; the 401-refresh `DelegatingHandler` pattern ports if/when refresh tokens exist. Absolute base URL + token auth per `docs/mobile-strategy.md` item 1 | #336 |
| Real-time client practice | — (the reference app has none) | **Net-new** | SignalR mobile lifecycle (foreground connect / background disconnect / `WithAutomaticReconnect` / resume re-sync) with a testable handler seam — `docs/mobile-strategy.md` item 2 | #336 |
| `TestSupport` project: builders, `FakeHttpMessageHandler`, `TestJwt`, `TestTimeProvider` | its `TestSupport` project | **Adapt** | Reuse the existing test harnesses/AutoFixture first; add mobile builders only where missing | #336 |
| Four named test tiers via `[Trait("Category", …)]`; "pick the lowest tier that catches the bug"; default filter excludes the device tier | `docs/engineering-conventions.md`, all test projects | **Port** | Category tags for the mobile tiers; `Category!=UI` default filter; mapped onto the existing project-per-layer taxonomy (deciders / Alba / bUnit / Playwright) | #337 |
| E2E without UI: boot the real API in-process, mock only persistence (~0.5 s, no live DB) | its `E2E.Tests` project (WebApplicationFactory) | **Adapt** | Already satisfied by Alba + Testcontainers (`Farkle.WebTests`); mobile services reuse that harness rather than a new one | #337 |
| Test-first on touched code; reproduce-first bug fixing; deterministic tests (`TimeProvider`, no sleeps) | `docs/engineering-conventions.md` | **Port** | Merge into CLAUDE.md conventions (repo already has TDD red/green; add the touched-code and hygiene rules) | #342 |
| 100% new-code coverage gate (`diff-cover` vs PR base) + HTML report, identical locally and in CI | `scripts/{coverage,diff-coverage,coverage-html}.sh`, `coverage.mobile.runsettings`, gate steps in `test-mobile.yml`/`api-tests.yml` | **Port** | `tests/scripts/` + a GitHub Actions gate job + artifact-published HTML report | #337 |

## CI/CD & release

| Practice | Reference artifact | Verdict | Target here | Owner |
|---|---|---|---|---|
| Fast device-free gate blocks the expensive device builds | `test-mobile.yml` (plain SDK agent, no MAUI workload) | **Port** | Linux `workflow_call` job; no workload install; required before device builds | #337 |
| Templated, parameterized pipeline stages | ADO `stages/templates` + `parameters` (`ci.yml` → `setup.yml`/`ios-build.yml`/…) | **Port (translate)** | Reusable workflows (`workflow_call` + inputs); shared toolchain-setup step | #338 |
| Cost-aware triggers: Android per-merge (Linux), iOS post-merge only (macOS), UI nightly cron | `ci.yml` conditions + `schedules:` | **Port** | Workflow `if:`/`on.schedule` equivalents | #338, #339 |
| MAUI workload install pinned to SDK version; Xcode pinned explicitly | `setup.yml`, `ios-build.yml` (`xcode-select`) | **Port** | Setup step honoring `global.json`; explicit Xcode pin on macOS runners | #338 |
| Signing material out of the repo: keystore / `.p12` / provisioning profile as secure files | ADO secure files + `DownloadSecureFile@1` | **Port (translate)** | Base64 GitHub secrets decoded at build; temp keychain on macOS; encode/rotate runbook | #338 |
| Monotonic Play-safe versioning `<yyyyMMdd><dailyRev>` with overflow guard | `ci.yml` counter vars + `Get-PlaySafeAndroidVersionCode` | **Port** | Same scheme from a run counter; shared by `versionCode` and `CFBundleVersion` | #338 |
| CD to TestFlight + Play internal track, triggered by the CI build's artifacts | `cd.yml` (`AppStoreRelease@1`, `GooglePlayRelease@4`, pipeline-resource trigger) | **Port (translate)** | `workflow_run`-triggered release workflow; App Store Connect API key + Play service-account JSON as secrets | #340 |
| Deploy approval gates per environment | ADO environments (`TestFlight`, `GooglePlayInternal`) | **Port (translate)** | GitHub environments with required reviewers (pattern already used by infra's `production` gate) | #340 |
| Validate pipeline YAML cheaply before pushing | ADO preview-run (`previewRun: true`) | **Adapt** | `actionlint` locally / pre-push; note in conventions | #342 |
| Don't double-trigger CI on merge (batched auto-build vs manual run) | CLAUDE.md CI-helper note | **Drop** | ADO-specific; GitHub Actions triggers don't have the batched-counter interplay | — |

## Device/UI testing & evidence

| Practice | Reference artifact | Verdict | Target here | Owner |
|---|---|---|---|---|
| Black-box device smoke tier, config **entirely via env vars** so local and CI run the same tests | its `UITests` project + README (`UITEST_*`) | **Adapt** | Small WebView-aware device smoke (launch / assets load / login / one action / one SignalR push). Driver decided by spike: Appium (the toolchain pinned by the reference app) vs Playwright-on-WebView. Tier is deliberately smaller — bUnit + Playwright + storyboard already cover the shared RCL | #339 |
| `AutomationId` selectors + documented cross-platform locator asymmetry | `UiWait.cs`, `LoginPage.cs` | **Adapt** | WebView content uses web selectors (ids/test-ids as in Playwright today); native-shell chrome only needs the reference app locator lore | #339 |
| Evidence-the-change: screenshots + short video of the real app attached to every user-facing PR, captured by driving the UI test | CLAUDE.md "Evidence the change" + capture gotchas | **Port** | Wrapper script + runbook; extend the existing `storyboard.yml` PR-comment pattern to mobile artifacts | #339 |
| Capture gotchas: pin `UITEST_UDID`; SIGINT (not kill) to finalize `simctl recordVideo`; run UI classes in isolation; kill stale WDA | CLAUDE.md §Evidence, UITests README | **Port** | `docs/` runbook (symptom → cause → fix, `lessons-learned.md` format) | #339 |
| Android emulator lore: arm64 AVD needs `-p:RuntimeIdentifier=android-arm64 -p:EmbedAssembliesIntoApk=true`; enable wifi/data via `adb svc`; disable autofill popup | CLAUDE.md §Local UI runs | **Port** | Same runbook; hosted-runner Android emulator marked `continue-on-error` until proven (the reference app paused theirs) | #339 |
| MFA-bypass test account for UI login | `ConfigSettings:MfaBypassEmails` | **Drop** | No OTP flow here; the seeded `player1@email.com` account already serves | — |
| Offline/local mode: app → local API | `scripts/start-local-api.sh`, `USE_LOCAL_API` overlay | **Adapt** | Shell config pointing the app at the local docker-compose backend; one-command local loop | #342 |

## Store & compliance

| Practice | Reference artifact | Verdict | Target here | Owner |
|---|---|---|---|---|
| Versioned per-store asset manifest produced before console work | `assets/`, `assets/play/` (icon-512, feature graphic, screenshots, descriptions) | **Port** | `assets/` manifest; screenshots produced by the evidence loop | #341 |
| Enrollment runbooks (Play Developer Program; Apple mirrored) | `docs/google-play-enrollment.md` | **Port** | Rewritten for a public consumer game + individual/org account choice | #341 |
| Play App Signing: Google holds the app key, CI keystore is the upload key | `docs/play-app-signing.md` | **Port** | Same model | #341 |
| Publisher credentials for CD (Play service account; App Store Connect API key) | `docs/play-publisher-service-account.md`, its variable group | **Port** | GitHub secrets + creation runbooks | #341, #340 |
| Data safety / content rating as **legal declarations** needing owner sign-off | `docs/play-data-safety.md`, `docs/play-content-rating.md` (⚠️ VERIFY pattern) | **Port** | Consumer-game answers (accounts, feedback); explicit sign-off step | #341 |
| iOS privacy manifest | — (predates the reference app docs) | **Net-new** | `PrivacyInfo.xcprivacy` runbook — common Hybrid rejection cause (ADR 0008) | #341 |
| Managed-private distribution channel (MDM/EMM discovery) | `docs/user-stories/android-distribution-user-stories.csv` (AND-02/03) | **Drop** | Enterprise-only concern; HotDice is a public listing | — |

## Tooling, conventions & process

| Practice | Reference artifact | Verdict | Target here | Owner |
|---|---|---|---|---|
| Test-on-edit **Stop hook**: fast tests run when testable code changed; exit 2 feeds failures back; re-block guard | `scripts/claude-test-on-edit.sh` + `.claude/settings.local.json` | **Port** | `.claude/hooks/` + tracked `.claude/settings.json` (alongside the existing SessionStart hook) | #342 |
| Slash commands encoding the per-story loop (`/new-us`, `/pr`, `/run-verify`) | `~/.claude/commands/*.md` (user-level) | **Adapt** | **Repo-scoped** `.claude/commands/` GitHub variants (`Closes #N`, DoD template) | #342 |
| Shared conventions doc + personal-workflow split; **agent autonomy boundaries** (stop-and-ask list) | `docs/engineering-conventions.md`, `CLAUDE.local.md` | **Adapt (merge)** | Fold the deltas into this repo's CLAUDE.md — single source, no fork | #342 |
| PR template rendering the Definition of Done | `.azuredevops/pull_request_template.md` | **Port (translate)** | `.github/pull_request_template.md`, merged with existing PR standards (warnings-as-errors, generated files, `Closes #N`, storyboard evidence) | #342 |
| Backlog-as-code: YAML source of truth + idempotent tracker importer | `backlog/items.yaml`, `scripts/import-backlog.ps1`, `BACKLOG.md` | **Drop** | GitHub issues + sub-issues + `gh` already serve as source of truth here; an ADO importer has no target. Epic/sub-issue structure replaces it | — |
| Runbooks indexed in `docs/` (symptom → cause → fix) | `docs/README.md`, `docs/ios-simulator-codesign-crash.md` | **Port** | Continue `docs/lessons-learned.md` format; add a docs index as the count grows | #339, #341 |

## ADO-only mechanisms → GitHub equivalents

Mechanisms that live in Azure DevOps itself (not the reference app repo) and what plays their role here:

| ADO mechanism | Role | GitHub equivalent |
|---|---|---|
| Shared variable group | Shared secret/config bundle across pipelines | Repo/organization **Actions secrets** + **environment** secrets for deploy-scoped values |
| Secure files (keystore, `.mobileprovision`, `.p12`, Play JSON) | Binary secrets downloaded at build | Base64-encoded secrets decoded in the workflow (+ temp keychain on macOS) |
| Service connections (App Store, Google Play tasks) | Store authentication | App Store Connect API key + Play service-account JSON as secrets, used by upload actions/fastlane |
| Environments + approvals (`TestFlight`, `GooglePlayInternal`) | Manual deploy gates | GitHub **environments** with required reviewers (already used for infra `production`) |
| Pipeline `schedules:` cron | Nightly iOS UI run | `on: schedule` cron |
| Pipeline-resource trigger (`cd.yml` on CI completion) | CD follows CI artifact | `workflow_run` trigger + `actions/download-artifact` |
| Work items, `AB#<id>` links, `us_<id>/…` branches | Tracking + branch↔work linkage | Issues + sub-issues, `Closes #<id>` (already the repo rule), issue-numbered branches |
| Preview-run YAML validation | Cheap pipeline linting | `actionlint` (no server-side preview needed — GH workflows have no ADO template-expansion semantics) |
| Build counter (`counter(DateStamp, 1)`) | Daily revision for versioning | `github.run_number` (or a date-scoped counter derived in the workflow) |

## Candidate runbooks from the reference app tacit knowledge

Hard-won gotchas to land as `docs/` runbooks when their step executes (format of
[`lessons-learned.md`](lessons-learned.md)):

1. **iOS simulator `CODESIGNING 2 Invalid Page` crash** — stale build artifacts, not code (its `docs/ios-simulator-codesign-crash.md`). → #339
2. **Recording device runs** — pin the UDID you drive (else the driver clones its own sim and you record an idle screen); SIGINT to finalize `.mov`; reboot the sim if "recording already in progress". → #339
3. **Appium toolchain pinning** — appium 3.5.2 / uiautomator2 8.1.0 / xcuitest 11.17.7 proven together; kill stale WDA/xcodebuild between runs. (Applies if the Appium driver wins the spike.) → #339
4. **Android emulator on arm64 Macs** — arm64 AVD + `EmbedAssembliesIntoApk=true` or SIGABRT "No assemblies found"; `ANDROID_HOME` exported before Appium; `svc wifi/data enable` for network; autofill popup off. → #339
5. **UI test classes must run in isolation** — parallel classes fight over one simulator's WebDriverAgent. → #339
6. **Hosted-runner mobile emulators are the finicky part** — expect iterations; keep the local device loop primary (the reference app's Android UI stage is paused on hosted agents to this day). → #339

## What Farkle already had (no port needed)

For completeness — the reference app practices already satisfied here in a different form: per-PR CI with
test + generated-code drift gates (`e2e-happy-path.yml`: `verify-generated`/`verify-codegen`),
visual evidence on PRs (`storyboard.yml`), CodeQL, ADR log (`docs/decisions/`),
`docs/lessons-learned.md`, ops runbook (`infra/OPERATIONS.md`), SessionStart hook
(`.claude/hooks/session-start.sh`), TDD red/green commit convention, close-issues-via-PR,
branch-from-fresh-main, Dependabot.
