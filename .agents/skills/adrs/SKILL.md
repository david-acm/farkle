---
name: adrs
description: Consult and search HotDice Architecture Decision Records (ADRs 0001 through 0011) and core architectural invariants.
---

# HotDice Architecture Decision Records (ADRs)

Consult this skill when designing features, verifying architectural boundaries, or checking architectural conventions in HotDice.

## Available ADRs in `docs/decisions/`

1. **[0001 - Vertical slices and pure deciders](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0001-vertical-slices-and-pure-deciders.md)** *(Superseded by 0004)*
   * Established vertical slices and pure decision functions. The framework-free isolation was superseded by ADR 0004.
2. **[0002 - Marten stream identity](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0002-marten-stream-identity.md)** *(Accepted)*
   * Stream identity is a string key derived from the game code: `"game-{code}"` (e.g., `"game-992615"`).
3. **[0003 - Marten + Wolverine integration shape](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0003-marten-wolverine-integration-shape.md)** *(Superseded by 0004)*
   * Spike findings on Marten 9 / Wolverine 6 registration, assembly scanning, and runtime behaviors.
4. **[0004 - Marten-native domain](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0004-marten-native-domain.md)** *(Accepted)*
   * `GameState` is the Marten aggregate snapshot directly. Wolverine.HTTP endpoints bind aggregate state via `[WriteAggregate]` and invoke pure deciders directly without intermediary command buses or aggregate handlers.
5. **[0005 - Embrace SignalR in the core](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0005-embrace-signalr-in-the-core.md)** *(Accepted)*
   * SignalR notifications are returned directly from endpoints and broadcast via Wolverine's outbox. Extra adapter ports (`IGameEventBroadcaster`) were dropped.
6. **[0006 - Merge Contracts and SharedKernel](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0006-merge-contracts-and-sharedkernel.md)** *(Accepted)*
   * Merged into `HotDice.Shared` to avoid artificial fragmentation.
7. **[0007 - Codegen and schema ownership](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0007-codegen-and-schema-ownership.md)** *(Accepted)*
   * Static JasperFx code generation for production builds (`Internal/Generated`). Marten manages its own document and event schema; EF Core manages ASP.NET Identity.
8. **[0008 - Mobile via MAUI Blazor Hybrid](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0008-mobile-via-maui-blazor-hybrid.md)** *(Accepted)*
   * Cross-platform mobile (iOS and Android) powered by .NET MAUI Blazor Hybrid reusing `HotDice.Ui` (Razor Class Library).
9. **[0009 - Mobile practice port for HotDice](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0009-mobile-practice-port-for-hotdice.md)** *(Accepted)*
   * Translation and porting of test tiers, device pipelines, and store processes from the private reference app.
10. **[0010 - Mobile testability architecture](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0010-mobile-testability-architecture.md)** *(Accepted)*
    * Decoupled architecture: shared core tested on desktop; thin MAUI host shell (`HotDice.Shell`) isolated from the primary solution.
11. **[0011 - Mobile UI test driver: Appium over WebView](file:///Users/davidchaparro/RiderProjects/Farkle-main/docs/decisions/0011-mobile-ui-test-driver.md)** *(Accepted)*
    * Appium driving the underlying platform WebView DOM for smoke and regression evidence.
