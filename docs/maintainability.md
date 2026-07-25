# Maintainability: before vs. after the Critter Stack migration

This records the code-health impact of migrating Farkle from **Eventuous + EventStoreDB +
FastEndpoints** to the **native Critter Stack** (Marten + Wolverine) with vertical slices (epic #295).
It's a point-in-time snapshot for the record — not a gate — so the numbers can be re-derived any time
with the commands below.

## What's compared

| | Commit | Architecture |
|---|---|---|
| **Before** | `b99525f` (#292) | Eventuous command service, EventStoreDB, FastEndpoints, a separate `Farkle.Endpoints` project |
| **After** | `191ff37` (`main`) | Marten `Inline` snapshot, Wolverine.HTTP endpoints-as-handlers, vertical slices, SignalR embraced in the core |

## How to reproduce

Complexity/size uses [`lizard`](http://www.lizard.ws) (`pip install lizard`), auto-detecting C#, with
**generated code excluded** (the Kiota client, Wolverine static codegen, and build output) so we measure
hand-written code only:

```bash
lizard src   -x "*/bin/*" -x "*/obj/*" -x "*/Generated/*" -x "*/Farkle.ApiClient/*"
lizard tests -x "*/bin/*" -x "*/obj/*"
```

Structure metrics come from the tracked project files:

```bash
git ls-files 'src/**/*.csproj' | wc -l                       # project count
git grep -h '<ProjectReference' -- 'src/**/*.csproj' | wc -l  # dependency edges
git grep -hE 'interface I[A-Z]' -- 'src/**/*.cs' | wc -l       # abstraction interfaces
```

## Production code (`src/`, generated excluded)

| Metric | Before | After | Δ |
|---|---|---|---|
| **NLOC** (non-comment lines) | 5,212 | **4,241** | **−971 (−18.6%)** |
| Functions | 304 | 266 | −38 (−12.5%) |
| Avg NLOC / function | 11.3 | 10.2 | −9.7% |
| Avg cyclomatic complexity (CCN) | 2.1 | 2.1 | flat (already low) |
| Functions with CCN > 15 | 2 | 2 | flat (same `GameService` response mapper) |

Nearly a fifth less hand-written production code for the same behavior — the payoff of endpoints-as-handlers
and removing the command-service / broadcaster-port indirection. Complexity was already low and stayed low.

## Test code (`tests/`)

| Metric | Before | After | Δ |
|---|---|---|---|
| NLOC | 4,881 | 4,889 | +8 (flat) |
| Test functions | 362 | **407** | **+45 (+12.4%)** |
| Avg NLOC / test | 10.0 | 8.6 | −14% |
| Test-to-production NLOC ratio | 0.94 | **1.15** | more coverage per line of app |

Same test *volume*, but **more, smaller, focused tests** — and the suite now outweighs the production code
it covers. That's the "easy to iterate" goal showing up in the *shape* of the suite, not just the count.

## Structure — coupling & indirection

| Metric | Before | After | Δ |
|---|---|---|---|
| Projects in `src/` | 9 | 7 | −2 (`Contracts`+`SharedKernel` → `Farkle.Shared`; `Farkle.Endpoints` deleted) |
| **Project-reference edges** | 14 | **9** | **−5 (−36%)** |
| Abstraction interfaces (`interface I…`) | 18 | 14 | −4 (removed ports incl. `IGameEventBroadcaster`) |
| Vertical-slice folders | — | 11 | — |

**36% fewer inter-project dependency edges**, two fewer projects, four fewer abstraction seams — a concrete
drop in structural coupling and indirection.

## Headline

**~19% less production code, ~36% fewer project-coupling edges, cyclomatic complexity flat-and-low, and a
denser, more granular test suite.** The move to the native Critter Stack made the codebase materially leaner
and less indirect while *increasing* test granularity.
