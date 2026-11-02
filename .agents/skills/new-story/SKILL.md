---
name: new-story
description: Start a new HotDice issue branch from origin/main, inspect acceptance criteria, and plan TDD implementation.
---

# Start a New Issue Branch (HotDice)

Follow this procedure when beginning work on a HotDice task or GitHub issue.

## Usage
Provide the issue number and a brief description:
`/<name> <issue-number> <brief-description>`

## Procedure

1. **Branch Preparation**:
   * Fetch latest changes from origin:
     ```bash
     git fetch origin
     ```
   * Create and switch to a new branch branching strictly from `origin/main` (never a stale local `main`):
     ```bash
     git checkout -b feature/<issue-number>-<kebab-case-description> origin/main
     ```
   * Verify the upstream tracking and base:
     ```bash
     git rev-parse --abbrev-ref --symbolic-full-name @{u} || git merge-base HEAD origin/main
     ```

2. **Issue Inspection & Acceptance Criteria**:
   * View the issue details using GitHub CLI:
     ```bash
     gh issue view <issue-number>
     ```
   * Extract and restate all acceptance criteria into your working plan. These criteria form the branch's **Definition of Done**.
   * Note for the eventual PR body: Must include `Closes #<issue-number>` (the keyword is required).

3. **Development Cadence (TDD & Boundaries)**:
   * **Red $\rightarrow$ Green:** Commit failing unit/integration tests first (*Red*), then write implementation to pass (*Green*).
   * **Compiler Strictness:** Ensure builds succeed with **warnings as errors**.
   * **Autonomy boundaries:** Stop and consult the user before modifying schemas, adding public endpoints, adding NuGet dependencies, or touching auth/security.
