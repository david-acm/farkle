# 6. Merge HotDice.Contracts + HotDice.SharedKernel into HotDice.Shared

Status: **Accepted**

## Context

Two small, dependency-free leaf projects were shared by the server core (`HotDice`) and the Blazor
WASM client (`WebApp.Client`):

- **`HotDice.Contracts`** — the wire DTOs (HTTP request/response shapes, also the SignalR message
  payloads the client deserializes verbatim).
- **`HotDice.SharedKernel`** — pure shared *behaviour* (`ScoreCalculator`, `TurnActionPolicy`,
  `GameStage`) reused by the client's live score/turn preview.

Both are zero-dependency and must be separate assemblies from the now Marten/Wolverine-native core
so the WASM client can reference them without dragging in a server framework. The open question was
one shared leaf vs two.

## Decision

Merge them into a single **`HotDice.Shared`** project. The two logical namespaces are preserved —
`HotDice.Contracts` (under `Contracts/`) and `HotDice.SharedKernel.*` (under `Turns/`, `Scoring/`) —
so no source `using` changed; only project references moved. The arch guardrails collapse to one
assembly (`HotDice.Shared` must stay web-framework-free and depend on no other HotDice project).

## Rationale

Both leaves are zero-dependency and shared by the same server+client audience, so keeping them as
two projects bought a conceptual boundary (contract vs. shared logic) without a mechanical one. In
the Critter Stack's low-ceremony spirit — don't split for tidiness without a payoff — a single
shared leaf is enough. The at-least-one-shared-leaf split from the core is what actually matters
(the WASM client needs framework-free shared code); its internal granularity does not.

## Considered and rejected

Keep the two projects separate to preserve the "wire contract vs. shared behaviour" distinction.
Defensible for a teaching repo, but the distinction survives as namespaces/folders inside
`HotDice.Shared`; a whole extra project to express it is more ceremony than the lesson is worth.

## Consequences

- One fewer project; four consumers (`HotDice`, `HotDice.Infrastructure`, `WebApp.Client`,
  `HotDice.ArchitectureTests`) now reference `HotDice.Shared`.
- The merged assembly's name differs from its two namespace roots — accepted; an assembly is a
  deployment unit, and the namespaces remain the logical API.
- No wire/behaviour change: DTO types and namespaces are identical, so `swagger.json` + the Kiota
  client are unaffected.
