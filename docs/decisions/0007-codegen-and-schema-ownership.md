# 7. Static codegen in production; Marten owns its schema, EF owns Identity

Status: **Accepted** (#305).

## Context

The Critter Stack generates handler/endpoint "glue" code (the `FetchForWriting` + append +
serialization plumbing around each decider/endpoint) and manages database schema. Two operational
decisions had to be pinned down for #305 (tooling & ops):

1. **When is that glue code generated?** JasperFx can compile it in-memory at first request
   (`TypeLoadMode.Dynamic`, needs Roslyn at runtime) or load pre-generated, committed code
   (`TypeLoadMode.Static`, no runtime Roslyn — faster, more predictable cold start).
2. **Who owns database schema?** Marten can create/patch its own event + projection tables; ASP.NET
   Identity uses EF Core migrations. Farkle runs both on **one** PostgreSQL.

## Decision

**Static codegen in real Production, Dynamic everywhere else; each store manages its own schema.**

1. **Codegen mode is environment-driven** (`AddJasperFx` in `src/WebApp/Program.cs`):
   - Real **Production** → `TypeLoadMode.Static` with `AssertAllPreGeneratedTypesExist = true`
     (fail fast at startup if the committed code is missing/stale), for a fast, Roslyn-free cold start.
   - **Development**, tests, and the OpenAPI `GetDocument` boot (the `NSwag` environment) → `Dynamic`
     (regenerated in-memory), so none of them depend on the committed code being current.
   - `opts.ApplicationAssembly = typeof(Program).Assembly` — the generated code compiles into the
     **WebApp** host assembly, so the Static loader must look there (not in `Farkle`, where
     `AddWolverine` is called and which JasperFx would otherwise default to).
2. **Committed generated code** lives in `src/WebApp/Internal/Generated/`; a **`verify-codegen`** CI
   job regenerates it (`codegen write` in the NSwag env — no DB, no WASM) and fails on drift, the exact
   analogue of `verify-generated` for the OpenAPI/Kiota client.
3. **Schema ownership is split.** **Marten manages its own schema** (`AutoCreate.CreateOrUpdate` — no
   hand-written event/projection migrations). **ASP.NET Identity keeps its EF migrations**
   (`src/Farkle.Infrastructure/Migrations/`), applied on startup. Operators use the **JasperFx CLI**
   (`dotnet run --project src/WebApp -- describe | resources | db-apply | projections`) against the real
   configuration.

## Consequences

- Production cold start does no runtime code generation; a stale/missing generated file is a **loud
  startup failure**, not a silent slow first request. The cost is the regenerate-and-commit discipline,
  enforced by `verify-codegen`.
- The OpenAPI extraction and every test boot stay on `Dynamic`, so a contributor never has to
  regenerate the codegen just to run tests or produce the OpenAPI doc — only when a handler/endpoint
  **signature** changes (which `verify-codegen` catches).
- One PostgreSQL, two schema owners: Marten (events + the `GameState` snapshot) auto-manages; EF
  (Identity) migrates. There is no hand-written migration for game data.
- The Static loader's assembly (`ApplicationAssembly = WebApp`) is load-bearing: without it, Static
  throws `ExpectedTypeMissingException` looking for the types in `Farkle`. (This bit the #305 PR's
  OpenAPI build before the `ApplicationAssembly` fix.)

Runbook: [`../../infra/OPERATIONS.md`](../../infra/OPERATIONS.md). Onboarding cheatsheet:
[`../critter-stack-onboarding.md` §5](../critter-stack-onboarding.md).
