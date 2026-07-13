# Agent Forge Copilot — CI/CD Setup Notes

The pipeline entry point is `.gitlab-ci.yml` at the repo **root** (GitLab
auto-discovers it — no project setting required), and the per-stage jobs live
under `.gitlab/`:

```
.gitlab-ci.yml            <- pipeline root: workflow, stages, defaults, includes
.gitlab/
└── ci/
    ├── lint.yml          <- lint stage: dotnet format + third-party license scan
    ├── build.yml         <- build stage
    ├── test.yml          <- unit + integration test stage
    ├── deploy.yml        <- deploy stage (auto on main)
    └── verify.yml        <- verify stage: post-deploy smoke test (auto on main)
```

The root file `include:`s the five fragments. Everything about *where* and
*how* the pipeline runs is defined in code — the only remaining items below are
GitLab **policy** settings (who can merge when), which have no in-file
equivalent.

## 1. Commit the files

Add `.gitlab-ci.yml` and the `.gitlab/ci/` folder to the repo. Before the first
run, check the three variables at the top of `.gitlab-ci.yml`:

| Variable | Default | What to set it to |
|---|---|---|
| `SOLUTION` | auto-detect | Your `.sln` path if the root has more than one solution/project |
| `UNIT_TEST_PATTERN` | `*UnitTests.csproj` | Whatever your unit test projects are actually named |
| `INTEGRATION_TEST_PATTERN` | `*IntegrationTests.csproj` | Same for integration test projects |

Both test jobs **fail loudly if zero projects match**, so a wrong pattern can't
silently produce a green pipeline.

Also adjust `ConnectionStrings__DefaultConnection` in `.gitlab/ci/test.yml`
(the `integration-tests` job) to match the config key your code reads. In .NET,
a double underscore maps to `:`, so `ConnectionStrings__DefaultConnection`
⇒ `ConnectionStrings:DefaultConnection` in `appsettings.json`.

> Tip: **CI/CD → Editor** validates and visualizes the merged pipeline
> (root + includes) — a fast way to confirm the `include:` paths resolve.

## 2. Block merging on red pipelines (requirement: "user must fix")

**Settings → Merge requests → Merge checks:**

- ✅ **Pipelines must succeed**
- ✅ **Skipped pipelines are considered failed** (closes a loophole)

Without this, a failing pipeline is only advisory — GitLab will still let the
MR merge. (This is a project policy setting; there is no in-repo equivalent.)

## 3. Re-test when other branches merge into main

Two layers cover this:

1. **In the YAML (works on any tier):** every MR job starts by merging the
   latest `main` into the MR head (the `before_script` in `.gitlab-ci.yml`). So
   whenever the pipeline runs, it tests *your changes + current main*, not a
   stale snapshot. A conflict with main fails the pipeline immediately with a
   clear message.
2. **Auto re-run when main moves:** GitLab only *automatically* re-runs MR
   pipelines on target-branch changes with **merged results pipelines / merge
   trains** (Premium features). Check **Settings → Merge requests → Merge
   options** — if you see **"Enable merged results pipelines"**, turn it on
   (and optionally merge trains). On the Free tier, the fallback is the layer-1
   merge plus hitting **Run pipeline** on the MR (or push/rebase) after main
   moves. "Pipelines must succeed" still guarantees nothing merges without a
   green run.

**Settings → CI/CD → General pipelines:** enable
**Auto-cancel redundant pipelines** so rapid pushes don't queue up stale runs.

## 4. Main must pass tests before deploy

Enforced by pipeline stage ordering: `deploy` runs after the `lint`, `build`, and `test` stages (with `verify`, the post-deploy smoke test, running last), and a failed
`lint`, `build`, `unit-tests`, or `integration-tests` job stops the pipeline
before `deploy` ever runs. `deploy` itself runs **automatically** on `main`
(`.gitlab/ci/deploy.yml`, `rules: if $CI_COMMIT_BRANCH == $CI_DEFAULT_BRANCH` —
no manual gate) and ships via `railway up --service agent-forge-api-staging --ci`; see
`RAILWAY.md` for the deployed target and rollback procedure.

## 5. Failed-test widget in MRs — done

`JunitXml.TestLogger` is added to both test projects (`Directory.Packages.props`),
`.gitlab/ci/test.yml` logs `--logger "junit;LogFileName={assembly}.junit.xml"`,
and the `reports: junit:` block in the shared test template is live. Failed
tests surface inline in the MR, not just in job logs.

## What else fits in `.gitlab/`

Since you're consolidating, GitLab natively recognizes these subfolders too, if
you ever want them:

- `.gitlab/merge_request_templates/*.md` — MR description templates
- `.gitlab/issue_templates/*.md` — issue templates
- `.gitlab/agents/` — GitLab Agent for Kubernetes config

## How the flow looks day-to-day

1. Dev opens an MR → pipeline runs lint → build → unit → integration. Red
   pipeline = merge button locked until they push a fix.
2. Dev pushes more commits → pipeline re-runs automatically (old run auto-cancels).
3. Someone else merges to main → this MR's next run tests against the new main
   (auto-triggered on Premium; manual "Run pipeline"/rebase on Free).
4. MR merges → `main` pipeline runs the full suite again on the merged result.
5. Main is green → `deploy` becomes a click-to-run manual job. Main is red →
   deploy is unreachable.
