---
description: Start a new HotDice issue branch from fresh origin/main
argument-hint: <issue-number> <brief task description>
---

Start work on issue **#$1**.

1. `git fetch origin`, then create and switch to a branch off **`origin/main`** (never a stale local `main` or an unrelated branch) named `feature/$1-<brief-kebab-description>` — derive the description from: $ARGUMENTS.
2. Confirm the new branch's base really is `origin/main`.
3. Read issue #$1 (`gh issue view $1`) and restate its acceptance criteria as the plan — those criteria are the Definition of Done for this branch.
4. Record that the PR body must contain `Closes #$1` (the keyword — a bare `#$1` does not close it).

Then implement per `CLAUDE.md`: TDD Red→Green commits (failing tests only in the first commit), test-first on touched code, warnings as errors, and the autonomy boundaries — stop and ask before schema changes, a new public endpoint, a new dependency, destructive/irreversible operations, or anything touching auth, security, or secrets.
