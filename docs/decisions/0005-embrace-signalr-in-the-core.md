# 5. Broadcast over SignalR directly from the core — drop the IGameEventBroadcaster port

Status: **Accepted**

## Context

Real-time updates are pushed to players over SignalR. Until now the core (`HotDice`) triggered a
broadcast through an `IGameEventBroadcaster` **port** (in `HotDice.Application`), implemented by a
`SignalRGameEventBroadcaster` **adapter** in `HotDice.Infrastructure` that wrapped
`IHubContext<GameHub>`. `GameNotifier` (core) loaded the up-to-date Marten snapshot, mapped it to a
response DTO, and called the port; the adapter did the `IHubContext` send.

That port existed to keep SignalR out of the core. But [ADR 0004](0004-marten-native-domain.md)
already made the core **Marten- and Wolverine-native** — it embraces the persistence/messaging
frameworks directly rather than hiding them behind abstractions. Against that backdrop the
broadcaster port was the last remaining single-implementation interface whose only job was to invert
a dependency the Critter Stack says not to invert (Jeremy Miller, *We Don't Need No Stinkin'
Repositories* / *The Case Against Clean Architecture*).

## Decision

Broadcast over SignalR **directly from the core**. `GameNotifier` now injects
`IHubContext<GameHub>` and does the `hub.Clients.Group($"game-{id}").SendAsync(...)` itself. The
`IGameEventBroadcaster` interface and the `SignalRGameEventBroadcaster` adapter are deleted, and
`GameHub` moves into `HotDice` (`HotDice.Realtime`). `HotDice` already carries a
`FrameworkReference` to `Microsoft.AspNetCore.App`, so no new dependency is added — SignalR was
already reachable, only walled off by the arch test.

The `KeepTheCoreFreeOfInfrastructureLibraries` guardrail drops `Microsoft.AspNetCore.SignalR` from
its forbidden list (EF Core and ASP.NET **Identity**, the auth stack, stay out — those aren't part
of the game's core behaviour).

## Consequences

- One fewer interface and one fewer class; the load → map → send path is a single method on
  `GameNotifier` instead of a notifier + a port + an adapter.
- The core now references SignalR types (`IHubContext`, `GameHub`) — consistent with it already
  referencing Marten and Wolverine. "Embrace, don't abstract" applied to realtime.
- The Wolverine static codegen was regenerated: the broadcast handlers now resolve
  `IHubContext<GameHub>` instead of the deleted port.
- The trade is explicit: a little less isolation for a little less ceremony. Auth (Identity) stays
  behind the `HotDice.Infrastructure` boundary because it is genuinely a separate concern from the
  game; realtime is not — it is how the game *is* played.

## Alternative considered

Keep SignalR out of the core by moving `GameNotifier` + the broadcast handler into
`HotDice.Infrastructure` and injecting `IHubContext` there (removing the port but not the boundary).
Rejected as the more contorted option — it preserves an isolation boundary the core had already
abandoned for Marten/Wolverine, at the cost of relocating the notifier and reconfiguring Wolverine's
handler discovery.
