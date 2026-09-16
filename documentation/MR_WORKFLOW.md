# MR_WORKFLOW.md — how a change gets from a branch to `main`

The lifecycle every change moves through, and who acts at each step. Written down because the
[Coordinator](agents/coordinator.md) drives it, and a role that drives an undocumented process is inventing
one.

**The live tracker is GitHub** (`INDEX.md` §5): issues and pull requests are both `#N`. Legacy `gitlab#N`
and `!N` citations are historical context, not links.

## The states

| State | What it means | Who moves it on |
|---|---|---|
| **Issue open** | A tracking issue exists, before any branch. No orphaned MRs — root `AGENTS.md` | human, or Coordinator |
| **Branch** | `feat/…`, `fix/…`, `chore/…`, `docs/…` off `develop`, ideally in its own worktree | Coding Agent |
| **Local gates** | `dotnet format --verify-no-changes`, unit + eval tests, and any gate the change touches | Coding Agent |
| **PR open** | Targets `develop`, cites the issue (`Closes #N` / `Related to #N`), describes what was verified | Coding Agent |
| **Reviewed** | The author spawns a Code Reviewer and blocks on its verdict; `review-verdict` gates on it (`CI-SETUP.md` §7–§8) | Coding Agent spawns it |
| **Changes requested** | The reviewer ruled `Request changes`, or a human asked | Coordinator dispatches a Coding Agent per finding |
| **Green** | `STATE=approved`, gates pass, PR description matches what landed | Coordinator marks ready |
| **Merged** | Merge commit to `develop` (this repo does not squash) | **human only** — gh#401 revisits this |
| **Promoted** | `develop` → `main` in its own MR | human |
| **Issue closed** | Closed by the merge, or by hand when the MR targeted a non-default branch | human, or Coordinator |

## The rules that are not obvious

- **Green CI means the gates ran.** Every branch pushed to `origin` gets the full pipeline — lint, build,
  test, evals, and both doc gates (`CI-SETUP.md`). **No CI job reviews code** (§7) — the reviewer is spawned
  by the author — so a green run says the gates passed, not that anything reviewed the change. The
  `review-verdict` gate is what keeps an unreviewed PR from looking done (§8).
- **An approval is about one revision.** A push after a verdict makes the MR unreviewed again; the reviewer
  names the head SHA for exactly this reason.
- **Nothing automated merges, and nothing automated approves.** The reviewer's ruling is posted as a review
  whose state is `COMMENTED` (`CI-SETUP.md` §8): `review-verdict` reads the verdict *line*, while GitHub never
  counts it as an approval — so a bot can never satisfy an approval rule a human was meant to.
- **`develop` is the integration branch.** `main` trails it and is advanced by a promotion MR, not by feature
  branches.
- **Docs move in the same change**, and not only the nearest file: grep the concept and update every doc that
  describes it.

## Where it stalls, and what that means

A change that has been round the changes-requested loop **twice** without going green is not a prompting
problem. Either the task was sized wrong ([`agents/task-sizing.md`](agents/task-sizing.md)) or the finding is
really a disagreement about what the change should be — which is a human decision, not another iteration.
