# 🧪 Test projects

Seven xUnit projects, each owning one layer of the test pyramid. The rule of thumb from the root
`CLAUDE.md`: prove a rule once, at the lowest layer that can observe it, and let the layers above
prove only what they add (an HTTP status, a rendered DOM, a real browser round trip).

## 📋 What each project covers

| Project | Tests | ⏱️ Runtime | 🔌 Needs | ❓ Question it answers |
|---|---:|---:|---|---|
| 🎲 `HotDice.Tests` | 176 | < 1 s | nothing | Do the game rules, deciders, scoring and turn policy behave? |
| 🧩 `HotDice.SpaTests` | 125 | ~2 s | nothing (bUnit) | Do UI state handlers, API wrappers and the theme behave, without a browser? |
| 🌐 `HotDice.WebTests` | 50 | ~11 s | 🐳 Docker (Testcontainers Postgres) | Does each HTTP slice turn commands into the right status, body, events and broadcasts on the real host? |
| 📱 `HotDice.Client.Tests` | 16 | < 1 s | nothing | Do the rules shared by the mobile and web clients (lifecycle resync, API client factory) hold? |
| 🎯 `Blazor.Dice.Tests` | 16 | < 1 s | nothing | Does the dice component compute its 3D rotations correctly? |
| 🏛️ `HotDice.ArchitectureTests` | 13 | ~1 s | nothing (ArchUnitNET) | Are the dependency and purity rules of the architecture still respected? |
| 🎭 `HotDice.E2eTests` | 11 | ~3 min | 🐳 Docker + Playwright Chromium | Can a real user complete a two-player game in a real browser? |

Counts and timings are from a full `dotnet test HotDice.sln` run on 2026-10-24.

## 🗺️ Coverage at a glance

One row per project, one column per layer of the system. 🟢 means the project **owns** that layer
and new assertions for it belong there. 🟡 means the layer's code **executes indirectly** while the
project tests something else. ⚪ means the layer never runs there.

| Project | 🎲 Domain | 🌐 HTTP host | 🧩 UI logic | 📱 Client core | 🏛️ Architecture | 🖥️ Browser |
|---|:-:|:-:|:-:|:-:|:-:|:-:|
| 🎲 `HotDice.Tests` | 🟢 | ⚪ | ⚪ | ⚪ | ⚪ | ⚪ |
| 🧩 `HotDice.SpaTests` | 🟡 | ⚪ | 🟢 | ⚪ | ⚪ | ⚪ |
| 🌐 `HotDice.WebTests` | 🟡 | 🟢 | ⚪ | ⚪ | ⚪ | ⚪ |
| 📱 `HotDice.Client.Tests` | ⚪ | ⚪ | ⚪ | 🟢 | ⚪ | ⚪ |
| 🎯 `Blazor.Dice.Tests` | ⚪ | ⚪ | 🟢 | ⚪ | ⚪ | ⚪ |
| 🏛️ `HotDice.ArchitectureTests` | ⚪ | ⚪ | ⚪ | ⚪ | 🟢 | ⚪ |
| 🎭 `HotDice.E2eTests` | 🟡 | 🟡 | 🟡 | 🟡 | ⚪ | 🟢 |

Read it top to bottom as the pyramid: the first row is the fastest and the most isolated, the last
row is the slowest and runs everything. Read it left to right to see where a failure in a given
layer will show up: a broken scoring rule turns the 🎲 column red in three projects, but only the
first one is where to fix the test.

## 🔬 Full coverage matrix

Rows are concerns in the system. Columns are the projects. ● means that project is the **owner**
of that concern. ○ means the project executes that code as a by-product of exercising something
else, so a regression there may surface in that column too, but it should be reproduced and fixed
at the owning layer.

| Concern | 🎲 Tests | 🧩 SpaTests | 🌐 WebTests | 📱 Client.Tests | 🎯 Blazor.Dice.Tests | 🏛️ ArchTests | 🎭 E2eTests |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **🎲 Domain** | | | | | | | |
| Game aggregate rules (join, start, roll, keep, set aside, return, pass, win) | ● | | ○ | | | | ○ |
| Deciders: command + state → events, one per feature | ● | | ○ | | | | ○ |
| Rebuilding `GameState` from the event stream | ● | | ○ | | | | ○ |
| Scoring (`ScoreCalculator`, tricks, keepable dice) and the `MachinePlayer` | ● | | ○ | | | | ○ |
| Turn action policy (what the current player may do) | ● | ○ | ○ | | | | ○ |
| Player colours (palette assignment in join order) | ● | | ○ | | | | ○ |
| Feedback request validation rules | ● | | ○ | | | | |
| Application telemetry: structured game events | ● | | | | | | |
| **🌐 HTTP host** | | | | | | | |
| HTTP slices: one endpoint per command, status and body | | | ● | | | | ○ |
| Invalid requests mapped to 400 `ProblemDetails` | ○ | | ● | | | | |
| JWT bearer auth on protected endpoints | | | ● | | | | ○ |
| Server broadcasts after each command (GameBegan, DiceRolled, TurnChanged, LobbyChanged) | | | ● | | | | ○ |
| Marten event persistence and the `GameState` snapshot on Postgres | | | ● | | | | ○ |
| Wolverine handler compilation and concurrent commands on one stream | | | ● | | | | ○ |
| CORS, liveness, readiness and version endpoints | | | ● | | | | ○ |
| Domain event telemetry tagging in Marten records | | | ● | | | | |
| Game id generation and collision retry | | | ● | | | | ○ |
| **🧩 UI logic (no browser)** | | | | | | | |
| UI state handlers (BlazorState: roll, keep, pass, lobby, restore, error modal) | | ● | | | | | ○ |
| UI API service wrappers and the typed `HotDiceApiClient` over mocked HTTP | | ● | ○ | | | | ○ |
| UI telemetry behaviour in the BlazorState pipeline | | ● | | | | | |
| Component architecture and icon button accessible names | | ● | | | | | |
| Theme contrast (WCAG text, non-text, disabled states) | | ● | | | | | |
| App version display | | ● | | | | | ○ |
| Dice component rotation maths | | | | | ● | | ○ |
| **📱 Client core (shared by mobile and web)** | | | | | | | |
| App lifecycle: connect on start, disconnect in background, reconnect then resync | | | | ● | | | ○ |
| API client factory over a fake `HttpMessageHandler` | | | | ● | | | |
| **🏛️ Architecture** | | | | | | | |
| Dependency direction, decider purity, shared kernel as a leaf, ports implemented | | | | | | ● | |
| **🖥️ Browser** | | | | | | | |
| SignalR turn flip between two browsers without refresh | | | ○ | ○ | | | ● |
| Lobby: host start, second player join by share code, roster update | ○ | ○ | ○ | | | | ● |
| WASM hydration, landing page, nav drawer on mobile viewport | | | | | | | ● |
| State restore after page refresh | | ○ | ○ | | | | ● |
| Feedback widget (rage burst, submit) | ○ | ○ | ○ | | | | ● |
| Roll animation race in the browser | | ○ | | | | | ● |
| Visual storyboard capture at three viewports | | | | | | | ● |

● owner  ○ executed indirectly

### 🧭 Reading the columns

- 🎲 **Tests** runs nothing but in-memory domain code. Its only indirect cells are the feedback
  validator (the HTTP mapping of that validation is owned by WebTests), the lobby rules inside the
  aggregate, and the feedback domain events.
- 🧩 **SpaTests** never renders a full page. Handler tests drive BlazorState directly, service tests
  call the real `HotDiceApiClient` over a mocked `HttpMessageHandler`, and the rotation calculator
  is mocked, so the dice maths is not executed here. The turn policy from the shared kernel is
  consulted by the TurnActions handler, hence the hollow cell.
- 🌐 **WebTests** boots the real host on Postgres and drives every endpoint through the same
  `HotDiceApiClient` the UI uses, so the whole domain executes under it, including scoring (the
  set-aside slice picks dice with the `MachinePlayer`) and colours (the lobby roster). It owns the
  server side of the realtime story: that each command produces the right broadcast. It never
  opens a SignalR client, renders UI, or runs the client lifecycle.
- 📱 **Client.Tests** uses fakes for the realtime connection and the HTTP handler, so it touches the
  realtime contract (connect, disconnect, resync ordering) without a hub or a server.
- 🎯 **Blazor.Dice.Tests** is a single pure calculator and touches nothing else.
- 🏛️ **ArchitectureTests** loads the compiled assemblies and inspects their dependency graph. No
  application code runs, so it executes nothing indirectly by design.
- 🎭 **E2eTests** is wide on purpose. Its three happy-path tests play a complete two-player game to a
  win in two real browsers against the real host and Postgres, with the `MachinePlayer` choosing
  which dice to keep, so nearly every rule, slice, handler and component runs under them. It still
  owns only the browser-level concerns. What it never reaches: invalid-request paths (the UI never
  sends an invalid command), either telemetry sink, the architecture rules, the API client factory
  and the pure contrast maths.

## ▶️ How to run

```bash
dotnet test tests/HotDice.Tests/HotDice.Tests.csproj                 # ⚡ fastest loop
dotnet test HotDice.sln                                              # 🐳 everything, needs Docker
dotnet test tests/HotDice.E2eTests/HotDice.E2eTests.csproj --filter "FullyQualifiedName~GameHappyPath"
```

🎭 `HotDice.E2eTests` installs Chromium after build through the Playwright CLI global tool
(`dotnet tool install -g Microsoft.Playwright.CLI`). 🐳 `HotDice.WebTests` accepts an existing
Postgres through the `FARKLE_TEST_PG` connection string, otherwise it starts a Testcontainers one.

📊 Coverage helpers live in `scripts/` (`coverage.sh`, `coverage-html.sh`, `diff-coverage.sh`).
