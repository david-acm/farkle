---
description: Open a PR for the current branch using the Definition-of-Done template
---

Open a pull request against `main` for the current branch.

1. Review the full diff (`git diff origin/main...HEAD`) before writing anything — describe what actually changed, not what was intended.
2. Fill `.github/pull_request_template.md`:
   - **What & why** — reviewer-facing: the problem and the reasoning, not a restatement of the diff.
   - **Issue** — `Closes #<id>`, taking the id from the branch name (`feature/<id>-...`). The keyword is required.
   - **Definition of Done** — tick only what is genuinely done; leave a box unticked with a one-line note rather than ticking it optimistically.
   - **Verification** — name the flow you drove and what you observed. Attach visual evidence for user-facing changes. If a step was skipped or a check failed, say so here.
3. Push the branch and open the PR.
4. Subscribe to PR activity immediately so CI results and review comments arrive without polling; report the CI verdict when it lands.

Never add AI/Claude attribution to commits or PR text — the work is authored by the human.
