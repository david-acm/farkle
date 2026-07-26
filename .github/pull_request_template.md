<!-- Written for reviewers: what changed and *why*, not a restatement of the diff. -->

## What & why

## Issue

Closes #<!-- issue number — the keyword is required; a bare #N does not close the issue -->

## Definition of Done

- [ ] Tests at the right layer and green — decider/unit for domain logic, integration for a new slice, at least one E2E happy path for a new feature (see `CLAUDE.md` → Testing Patterns)
- [ ] TDD cadence followed where applicable — Red commit (failing tests only) before Green
- [ ] Build clean with **warnings as errors**
- [ ] Generated files regenerated + committed if contracts or handlers changed — `swagger.json` + `Farkle.ApiClient/` (`verify-generated`), `Internal/Generated` (`verify-codegen`)
- [ ] No stored V1 event modified (new version instead); no secrets committed
- [ ] UI change: storyboard capture reviewed, **no-scroll** holds at all three viewports, no test-selected button labels renamed
- [ ] Docs / runbooks / ADRs updated in this PR if behaviour or a decision changed

## Verification

<!--
How this was actually exercised — not "tests pass". Name the flow you drove and what you observed.
For user-facing changes, attach the visual evidence (storyboard frames; screenshots/video for mobile).
For pure CI/config/docs changes with no testable surface, say so and note how it was checked instead.
Report faithfully: if a step was skipped or a check failed, say that here.
-->
