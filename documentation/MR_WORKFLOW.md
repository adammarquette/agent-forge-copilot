# MR_WORKFLOW.md — how a change gets from a branch to `main`

The lifecycle every change moves through, and who acts at each step. Written down because the
[Coordinator](agents/coordinator.md) drives it, and a role that drives an undocumented process is inventing
one.

**The live tracker is GitLab** (`INDEX.md` §5): issues are `#N`, merge requests are `!N`.

## The states

| State | What it means | Who moves it on |
|---|---|---|
| **Issue open** | A tracking issue exists, before any branch. No orphaned MRs — root `AGENTS.md` | human, or Coordinator |
| **Branch** | `feat/…`, `fix/…`, `chore/…`, `docs/…` off `develop`, ideally in its own worktree | Coding Agent |
| **Local gates** | `dotnet format --verify-no-changes`, unit + eval tests, and any gate the change touches | Coding Agent |
| **MR open** | Targets `develop`, cites the issue (`Closes #N` / `Related to #N`), describes what was verified | Coding Agent |
| **Reviewed** | The `code-review` job posts a verdict on every push (`CI-SETUP.md` §7) | automatic |
| **Changes requested** | The job failed on a blocking finding, or a human reviewer asked | Coordinator dispatches the fix |
| **Green** | Review clean, gates pass, MR description matches what landed | Coordinator marks ready |
| **Merged** | Squash-merged to `develop` | **human only** |
| **Promoted** | `develop` → `main` in its own MR | human |
| **Issue closed** | Closed by the merge, or by hand when the MR targeted a non-default branch | human, or Coordinator |

## The rules that are not obvious

- **Gates are local, not remote.** GitLab runs only the reviewer; the build/test/eval gates live on the GitHub
  mirror, which a GitLab-only branch never reaches (`CI-SETUP.md` §7). **A green MR pipeline does not mean the
  tests pass** — it means the reviewer had nothing blocking to say.
- **An approval is about one revision.** A push after a verdict makes the MR unreviewed again; the reviewer
  names the head SHA for exactly this reason.
- **Nothing automated merges, and nothing automated approves.** The review job cannot approve — GitLab has no
  "request changes" endpoint, so a red job *is* the change request, and a bot approval could satisfy an
  approval rule a human was meant to.
- **`develop` is the integration branch.** `main` trails it and is advanced by a promotion MR, not by feature
  branches.
- **Docs move in the same change**, and not only the nearest file: grep the concept and update every doc that
  describes it.

## Where it stalls, and what that means

A change that has been round the changes-requested loop **twice** without going green is not a prompting
problem. Either the task was sized wrong ([`agents/task-sizing.md`](agents/task-sizing.md)) or the finding is
really a disagreement about what the change should be — which is a human decision, not another iteration.
