# 1. Vertical slices with pure deciders; purity by arch-test, not by project boundary

Status: **Superseded by [ADR 0004](0004-marten-native-domain.md)** (#302). Originally Accepted (#301, epic #295).

> The vertical-slice organization and the pure decision logic survive, but the
> **framework-free guardrail** this ADR established (no Marten in the domain, purity enforced
> by an arch-test) was fighting the Critter Stack's grain and is reversed by ADR 0004: the
> domain references Marten, `GameState` becomes the aggregate, and the deciders become
> `[AggregateHandler]` methods. See ADR 0004 for the rationale.

## Context

The Critter Stack migration (#295) reorganizes the server from horizontal layers
(domain / application / endpoints projects) toward **vertical slices**: one folder per
game command holding everything for that use case. Event sourcing on Marten caps how
vertical we can go — the aggregate state, the event vocabulary, and the projection are
shared by every slice — but the command handling verticalizes cleanly.

We also want the domain to stay framework-agnostic. The classic way is an assembly
boundary (a pure `Farkle.Domain` project that references no framework). The Critter Stack
philosophy, however, is deliberately low-ceremony: handlers embrace `IDocumentSession` /
Wolverine directly rather than hiding them behind ports.

## Decision

- Command logic is expressed as **pure deciders**: `Decide(command, state) -> events`,
  one per slice under `src/Farkle/Features/<Command>/`. Validation-as-events lives in the
  decider (a broken precondition returns the `IErrorEvent`), so the decision is a pure
  function with no I/O and no mocks to test.
- Side effects stay **out** of the decider: e.g. the dice roll (`IRandom`) happens in the
  handler and the rolled dice are passed in; the dice count is a pure `GameState.DiceToRoll`.
- State that a decision needs is **on the state**, not peeked from event history: `PassTurn`
  reads a pure `GameState.HasActedThisTurn` flag instead of scanning `game.Current`.
- Purity is enforced by an **architecture test**, not an assembly boundary: `*Decider` types
  must reference no Eventuous / Marten / Wolverine / ASP.NET / FastEndpoints / Npgsql
  (`KeepDecidersPureAndFrameworkFree`), and slices point inward only
  (`KeepSlicesOffTheApplicationAndInfrastructureLayers`). This keeps the slice's locality
  (decider, and later endpoint + handler, colocated) without splitting each feature across
  projects.

## Consequences

- Decider tests are the bulk of the domain suite: arrange a state (via the pure
  `GameState.Fold`), call `Decide`, assert the emitted events — zero mocks.
- A slice namespace (`Farkle.Features.StartGame`) collides with its command
  (`Command.StartGame`); slices reference the command qualified (`Command.StartGame`).
  **Update (2026-07):** resolved by moving each command into the slice that owns it and suffixing it
  — `Features/StartGame/StartGameCommand.cs`. The name no longer collides with the namespace, the
  shared `Domain/GameAggregate/Command.cs` grab-bag is gone, and `PlayerId` (a value object, not a
  command input) moved out to `Domain/GameAggregate/PlayerId.cs`.
- The deciders are the durable artifact of the migration. The Eventuous aggregate/state/
  command-service that currently delegate to them are replaced wholesale by Wolverine's
  aggregate-handler workflow at the #302 cutover — the deciders carry over unchanged.
