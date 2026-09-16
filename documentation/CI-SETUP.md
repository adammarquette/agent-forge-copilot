# Agent Forge Copilot — CI/CD Setup Notes

CI runs on **GitHub Actions**. The pipeline entry point is
`.github/workflows/ci.yml`, which GitHub auto-discovers — no repository setting
required.

> **`origin` and CI are both GitHub.** The GitLab remote was dropped after its
> project was recreated empty by a restore (`INDEX.md` §5). Every branch pushed to
> `origin` gets the full pipeline below. **No CI job reviews code** (§7): the
> reviewer is spawned by the authoring agent, and the `review-verdict` gate waits
> for its ruling (§8). Nothing has to be mirrored anywhere first.

```
.github/
├── workflows/
│   ├── ci.yml                      <- lint + build + test + evals (this document)
│   └── railway-config.yml          <- Railway IaC: plan on PR, apply on merge,
│                                      scheduled drift check (DEPLOYMENT.md §9)
├── scripts/
│   ├── verdict-state.sh            <- the single verdict reader (see §8)
│   ├── post-verdict.sh             <- how the reviewer posts a readable ruling
│   ├── watch-verdict.sh            <- what the authoring agent blocks on
│   ├── verdict-state-selftest.sh   <- the accepted verdict shapes
│   └── verify-test-results.sh      <- shared false-green guard (see §5)
└── licenses/
    ├── allowed-licenses.json       <- license allowlist for the license gate
    └── license-packages-filter.json
```

> **Migration status.** This repo ran on GitLab CI until the move to GitHub. The
> **CI half** (lint, build, test, evals) is ported and live, and the **sidecar
> image is now published to GHCR** on merge to `main` (`publish-image`, below).
> Still **not ported**: the post-deploy `/health` + `/ready` smoke tests and the
> health-gated integration suite — they need repository secrets that do not exist
> yet and a running stack to point at. There is no CD half to port beyond that:
> the project has **no hosted environment**, so "deploy" means pulling the
> published image into a container stack, which the operator does (see
> [`DEPLOYMENT.md`](DEPLOYMENT.md)). A Railway IaC definition and its
> `railway-config.yml` workflow now exist in source but have **not** been applied
> — `DEPLOYMENT.md` §9. The retired `.gitlab-ci.yml` and `.gitlab/ci/`
> have been **deleted from the tree** (the code reviewer that briefly lived there
> was ported to `.github/` and then deleted outright, §7); the originals — including `deploy.yml`,
> whose comments encode the deploy incidents any future CD job should honor (the
> `Llm__ApiKey` drift, the deploy-log-stream false positive, the scope-array
> truncation) — live in git history at `fbbf07d`
> (`git show fbbf07d:.gitlab/ci/deploy.yml`).

## 1. What runs, and when

| Job | Stage | Gate |
|---|---|---|
| `format` | lint | `dotnet format --verify-no-changes` |
| `license-scan` | lint | fails on a dependency outside `allowed-licenses.json` |
| `doc-sizes` | lint | `scripts/check-doc-sizes.sh` — every `~tok` price in a routing table matches its file, and the gate's own self-test still reddens |
| `docs-sync` | lint | **PRs only.** Fails when `src/`, `reverse-proxy/`, `docker-compose.yml` or `.env.example` changed but `documentation/` did not. Opt out with a `docs: n/a - <reason>` line in the PR body — a bare `docs: n/a` is rejected. Detects *absent* doc edits, not wrong ones; see root `AGENTS.md`, “Docs stay in sync with the code”. Companion to `doc-sizes`: that one keeps prices honest, this one keeps the prose honest. |
| `review-verdict` | gate | **PRs only.** Waits (5m, short while bedding in) for the Code Reviewer's ruling — a *review* body whose first line is `**Verdict: Approve**` / `**Verdict: Request changes**`. Runs only after `build` and the test jobs, because nobody rules on a diff that does not compile. No verdict **waits** rather than failing; request-changes ends the wait. An approval binds to the PR's contribution, so a target-sync or a non-force rebase keeps it while a new commit, a conflict resolution or a force-push kills it. §8. |
| `nginx-config-lint` | lint | renders `nginx.conf.template` and runs `nginx -t` |
| `openemr-pin` | lint | `tools/verify-openemr-pin.sh` — the `external/agent-forge` submodule and both OpenEMR image pins must agree (`DEPLOYMENT.md` §1) |
| `build` | build | compile under warnings-as-errors |
| `unit-tests` | test | fully mocked suite |
| `eval-tests` | test | deterministic eval rubrics as xUnit theories |
| `evals` | test | golden-set **hard gate** (Core Req 6) |
| `publish-image` | publish | **main only**; builds the sidecar, verifies it, pushes to `ghcr.io/adammarquette/agent-forge-copilot` (`sha-<12>` + `main` + `latest`). Gated on `unit-tests`/`eval-tests`/`evals`. Uses the built-in `GITHUB_TOKEN` — no secret. |

Triggers are `pull_request` (any branch) and `push` to **`main` and `develop`**.
Concurrency cancels superseded runs on the same ref (the old `interruptible:
true`), except on those two integration branches, so every merge produces one
complete attributable run.

Both integration branches are in the push list because each is protected (§2) and
the commit that actually lands there needs verifying on its own. A PR run tests
the *merge preview*; with squash merges enabled, the commit that lands is a new
object that never existed when the PR ran, so "require branches to be up to date"
does not cover it. It also means a direct push to a protected branch can acquire
the required checks instead of being permanently unmergeable.

> This is an explicit list, **not** a catch-all. The GitLab pipeline deliberately
> had no bare `$CI_COMMIT_BRANCH` rule (legacy issue #40): every branch here gets a
> PR right after its first push, so a catch-all tested the same commits twice —
> once on the raw branch ref, once on the MR pipeline. Naming only the integration
> branches keeps that fixed; feature branches run via `pull_request` alone. Don't
> widen it.

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
  `docs-sync`, `license-scan`, `nginx-config-lint`, `build`, `unit-tests`, `eval-tests`,
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

## 7. Why no CI job reviews code

**The reviewer is spawned, not scheduled.** The agent that authored the change opens its PR, waits for the
gates to go green, then starts a Code Reviewer with its own context and blocks on the ruling
(`src/AGENTS.md`, `documentation/agents/code-reviewer.md`). There is no CI job that reviews code.

That is deliberate, and it was tried the other way first. A CI reviewer existed briefly (gh#395) and never
completed a single review: it needed `ANTHROPIC_API_KEY` as a repository secret, failed on workspace
scoping, then on billing, and sat red on every PR in between. It was removed in gh#403, which closes the
tracking issue gh#402. Two reasons it is not coming back on a timer:

- **A spawned reviewer needs no API key.** It runs in the operator's own session, so there is no credential
  in a repository secret — on a public repo — for a job that can silently stop working when a balance runs out.
- **A review that arrives after the authoring session ends lands in an empty room**, and gets addressed by a
  session that has to rebuild the reasoning from the diff. Blocking the author is cheaper than re-deriving.

`render_review.py` and its self-test — which turned a verdict JSON into a review body, and were host-neutral
and tested — are in git history rather than gone. If a CI reviewer is ever wanted again, start there rather
than rewriting them.

What makes the ruling *count* is §8.

## 8. The verdict gate

§7 spawns the reviewer; this is what makes its ruling *count*. Three scripts, one reader:

| Script | Who runs it | What it does |
|---|---|---|
| [`verdict-state.sh`](../.github/scripts/verdict-state.sh) | everything else | **The single reader.** Parses the verdict and decides fresh vs stale. Two parsers that drifted would let a PR look ruled to one caller and unruled to another. |
| [`post-verdict.sh`](../.github/scripts/post-verdict.sh) | the reviewer | `preflight` then `review`. Confirms through the reader rather than trusting the POST — **exit 0 is the only outcome that means you ruled.** It switches on the reader's `STATE=` field, not its exit code: the question here is *"can the gate read this?"*, so both verdicts exit 0, and `stale` exits 0 with a loud warning that the ruling does not yet bind. |
| [`watch-verdict.sh`](../.github/scripts/watch-verdict.sh) | the **authoring agent** | Blocks until a ruling exists. `0` approve · `1` changes requested · `2` no ruling the gate can read — which is *not* approval. |

**What counts is a REVIEW body**, first line `**Verdict: Approve**` or `**Verdict: Request changes**`.
Forgiving about emphasis, casing, a trailing period and trailing prose; strict about the word.
[`verdict-state-selftest.sh`](../.github/scripts/verdict-state-selftest.sh) is the list of accepted shapes,
and it drives the real parser rather than a copy.

**The near-misses** — a PR comment carrying the verdict line (a different endpoint, never read) and an
inline comment (which creates a review with an *empty* body). Both are perfectly visible to a human and
invisible to the gate, so the author's watcher waits out its deadline next to a ruling that does not count.
That is worse than silence, because it looks like a verdict. A successful post comes back as state
`COMMENTED`; that is the expected shape, not a misfire.

**Why the author blocks on it** (`src/AGENTS.md`): a review that arrives after the authoring session ends
lands in an empty room and is addressed by a session that must rebuild the reasoning from the diff.

## 9. Day-to-day flow

1. Dev opens a PR → `format`, `license-scan`, `doc-sizes`, `docs-sync`,
   `nginx-config-lint`, `build` run in parallel, then `unit-tests`, `eval-tests`, `evals`. Red run = merge button
   locked (given §2).
2. Dev pushes more commits → the run re-triggers and the superseded one cancels.
3. Someone merges to the target branch → "require branches to be up to date"
   forces an update, which re-runs against the new target.
4. PR merges → the `main` push run executes the same suite on the merged result.
5. **Deploy is the operator's step** — CI publishes the image; bringing it up is
   `docker compose`. See `DEPLOYMENT.md` §5–§6 for the deploy and rollback
   procedure.
