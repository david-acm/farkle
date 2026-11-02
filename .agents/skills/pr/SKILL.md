---
name: pr
description: Prepare, verify, and open a pull request for HotDice following the repository Definition-of-Done template.
---

# Open a HotDice Pull Request

Use this skill when preparing to open a PR for the current feature branch against `main`.

## Procedure

1. **Review Diff**:
   * Inspect the exact diff against `origin/main`:
     ```bash
     git diff origin/main...HEAD
     ```
   * Formulate the summary based on what actually changed and *why*, not merely a repetition of the code diff.

2. **Fill Definition-of-Done Template**:
   * Match `.github/pull_request_template.md`:
     * **What & why:** Reviewer-facing explanation of the problem, motivation, and solution.
     * **Issue:** `Closes #<id>`, using the issue ID extracted from `feature/<id>-...`.
     * **Definition of Done:** Tick only genuine completions. If any item is skipped or inapplicable, leave it unchecked with an explanatory note.
     * **Verification:** Concrete steps driven, observed output, and visual proof (e.g. storyboard frames or video) where applicable.

3. **Attribution & Push**:
   * Never insert AI/assistant attribution in commits or PR markdown text.
   * Push branch to remote:
     ```bash
     git push -u origin HEAD
     ```
   * Open the PR using GitHub CLI:
     ```bash
     gh pr create --template .github/pull_request_template.md
     ```
   * Check CI runs and monitor status until checks complete.
