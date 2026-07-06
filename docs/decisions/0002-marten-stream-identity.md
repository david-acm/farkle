# 2. Marten stream identity: string key derived from the game code

Status: **Proposed** (decision recorded in #301; applied at the #302 cutover)

## Context

Farkle identifies a game by an `int` code in `[100_000, 1_000_000)` that players type to
join (e.g. `992615`) — today an Eventuous `GameId : Id`. Marten event streams are keyed by
either `Guid` (`StartStream<T>()`) or `string` (`StartStream<T>(streamKey)`). The migration
(#302) must pick one, and the choice touches the HTTP routes, the read model, and the
Kiota client — all of which key on the same int today.

## Decision

Use a **string stream key derived from the game code**: `"game-{code}"` (e.g.
`"game-992615"`). The int code stays the user-facing and API identity; only the event-store
stream key is the derived string.

Rejected alternative: a `Guid` stream identity with the code stored as an indexed property.
It is more idiomatic for greenfield Marten, but it would force a second identifier through
the routes / read model / client for no benefit in a sample app whose identity is already
the short code users type.

## Consequences

- No change to the HTTP routes, request/response DTOs, or the Kiota client — the code
  remains the wire identity.
- `GameId` becomes a plain record wrapping the int (dropping the Eventuous `Id` base) at the
  #302 cutover; the `"game-{code}"` mapping lives at the Marten boundary, not in the domain.
- Greenfield data (no ESDB replay, per the epic), so there are no existing Guid-keyed
  streams to reconcile.
