# Agent Forge Copilot — CI/CD Setup Notes

CI runs on **GitHub Actions**. The pipeline entry point is
`.github/workflows/ci.yml`, which GitHub auto-discovers — no repository setting
required.

```
.github/
├── workflows/
│   └── ci.yml                      <- lint + build + test + evals (this document)
├── scripts/
│   └── verify-test-results.sh      <- shared false-green guard (see §5)
└── licenses/
    ├── allowed-licenses.json       <- license allowlist for the license gate
    └── license-packages-filter.json
```

> **Migration status.** This repo ran on GitLab CI until the move to GitHub. The
> **CI half** (lint, build, test, evals) is ported and live. The **CD half** —
> Railway deploys, the post-deploy `/health` + `/ready` smoke tests, and the
> health-gated integration suite — is **not yet ported**; it needs repository
> secrets that do not exist yet and it touches live infrastructure. The retired
> `.gitlab-ci.yml` and `.gitlab/ci/` are kept **temporarily and inertly** as the
> reference for that port (nothing on GitHub reads them) and should be deleted
> once CD lands. Until then, **deploys are manual**.

## 1. What runs, and when

| Job | Stage | Gate |
|---|---|---|
| `format` | lint | `dotnet format --verify-no-changes` |
| `license-scan` | lint | fails on a dependency outside `allowed-licenses.json` |
| `nginx-config-lint` | lint | renders `nginx.conf.template` and runs `nginx -t` |
| `build` | build | compile under warnings-as-errors |
| `unit-tests` | test | fully mocked suite |
| `eval-tests` | test | deterministic eval rubrics as xUnit theories |
| `evals` | test | golden-set **hard gate** (Core Req 6) |

Triggers are `pull_request` (any branch) and `push` to `main`, matching the old
GitLab `workflow.rules`. Concurrency cancels superseded runs on the same ref
(the old `interruptible: true`), except on `main`, so every merge produces one
complete attributable run.

The three lint jobs and `build` run in parallel; the three test jobs depend on
`build` so a compile error surfaces as one red job rather than four.

Check the variables in the workflow's `env:` block if project names change —
`SOLUTION`, `BUILD_CONFIG`, `UNIT_TEST_PATTERN`. `unit-tests` **fails loudly if
zero projects match** its pattern, so a wrong pattern cannot silently produce a
green run.

## 2. Block merging on red runs

GitHub's equivalent of GitLab's "Pipelines must succeed" is **branch protection
with required status checks**. Without it a failing run is only advisory and the
PR stays mergeable.

On **Settings → Branches → Branch protection rules** for `develop` and `main`:

- ✅ **Require status checks to pass before merging**, selecting: `format`,
  `license-scan`, `nginx-config-lint`, `build`, `unit-tests`, `eval-tests`,
  `evals`
- ✅ **Require branches to be up to date before merging** (see §3)

Status checks only become selectable **after the workflow has run at least
once** on the repo — so merge this workflow first, then configure protection.

## 3. Freshness against the target branch

The old GitLab pipeline ran a `git fetch` + `git merge` of the target branch in a
global `before_script` so a run tested "your changes + current target" rather
than a stale snapshot.

**Actions gives this for free.** A `pull_request` run checks out the *merge
commit* (`refs/pull/N/merge`) — already your branch merged with the current
target — and GitHub blocks the PR outright when that merge conflicts. The
preamble is gone, along with the `GIT_DEPTH` tuning that existed only to make
its merge-base lookup work.

What Actions does *not* do automatically is re-run a PR when the target branch
moves. **"Require branches to be up to date before merging"** (§2) closes that:
it forces an update-and-re-run before the merge button unlocks. This is the free
equivalent of GitLab's Premium-only merged-results pipelines.

## 4. Test reporting

GitLab rendered `reports: junit` inline in the MR. Actions has no built-in
equivalent, so:

- `verify-test-results.sh` writes pass/fail/skip counts to the **job summary**,
  visible on the run page
- raw JUnit + TRX XML uploads as a **workflow artifact** (7-day retention)

Deliberately **not** a third-party reporter action — that would add a
supply-chain dependency on every CI run for what is essentially cosmetics.

## 5. The false-green guard

`.github/scripts/verify-test-results.sh` runs after every test job and **fails
the job** if either (a) no `*.junit.xml` was produced, or (b) the combined
`tests="N"` count is zero.

This exists because `dotnet test` once ran **zero tests and still exited 0**
(legacy tracker issue #21): the GitLab build stage's artifacts could not carry
the NuGet global packages cache, so `Microsoft.NET.Test.Sdk.props`'s
`Condition="Exists(...)"` import silently failed, `IsTestProject` never got set,
and MSBuild's VSTest target was silently skipped.

The Actions port removes that root cause — every job restores in its own
container, so there is no cross-job artifact to be missing — but the guard stays
as a second line of defence (NFR-REL-2: a meaningful check, not an unconditional
pass). It is unit-tested against three cases: no results, zero-test results, and
results with real failures.

## 6. Why there is no shared build artifact

GitLab passed `**/bin/` + `**/obj/` from `build` to the test jobs so they could
use `--no-build`. That artifact was the direct cause of the issue-#21 false green
(above), forced a `dotnet restore` before every `--no-build` test anyway, and
dragged in ~100MB Playwright driver copies that had to be hand-excluded to stay
under the artifact size limit.

Actions instead shares the **NuGet package cache** via `actions/cache`, keyed on
`Directory.Packages.props` + all `*.csproj`, and each job restores and builds in
its own container. Uploading tens of thousands of small files through the
artifact API is slow and loses the executable bit; caching the packages is both
faster and removes the whole bug class.

## 7. Day-to-day flow

1. Dev opens a PR → `format`, `license-scan`, `nginx-config-lint`, `build` run in
   parallel, then `unit-tests`, `eval-tests`, `evals`. Red run = merge button
   locked (given §2).
2. Dev pushes more commits → the run re-triggers and the superseded one cancels.
3. Someone merges to the target branch → "require branches to be up to date"
   forces an update, which re-runs against the new target.
4. PR merges → the `main` push run executes the same suite on the merged result.
5. **Deploy is manual** until the CD half is ported — see the migration note at
   the top and `RAILWAY.md` for the deploy and rollback procedure.
