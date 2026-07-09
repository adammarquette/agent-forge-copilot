# Agent Forge Copilot — CI/CD Setup Notes

The pipeline entry point is `.gitlab-ci.yml` at the repo **root** (GitLab
auto-discovers it — no project setting required), and the per-stage jobs live
under `.gitlab/`:

```
.gitlab-ci.yml            <- pipeline root: workflow, stages, defaults, includes
.gitlab/
└── ci/
    ├── build.yml         <- build stage
    ├── test.yml          <- unit + integration test stage
    └── deploy.yml        <- manual deploy gate (main only)
```

The root file `include:`s the three fragments. Everything about *where* and
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

Already enforced by pipeline structure: on `main`, the `deploy` job sits in the
last stage as a **manual** action. If `build`, `unit-tests`, or
`integration-tests` fail, the pipeline stops and the deploy button is never
runnable. When everything is green, deploy shows up as a ▶ manual job — replace
the `TODO` in `.gitlab/ci/deploy.yml`'s `script` with your real deployment steps.

## 5. Nice-to-have: failed-test widget in MRs

Add the [JunitXml.TestLogger](https://www.nuget.org/packages/JunitXml.TestLogger)
NuGet package to your test projects, change `--logger trx` to
`--logger "junit;LogFilePath=TestResults/{assembly}.junit.xml"` in
`.gitlab/ci/test.yml`, and uncomment the `reports: junit:` block in the test
template. Failed tests then appear inline in the MR instead of only in job logs.

## What else fits in `.gitlab/`

Since you're consolidating, GitLab natively recognizes these subfolders too, if
you ever want them:

- `.gitlab/merge_request_templates/*.md` — MR description templates
- `.gitlab/issue_templates/*.md` — issue templates
- `.gitlab/agents/` — GitLab Agent for Kubernetes config

## How the flow looks day-to-day

1. Dev opens an MR → pipeline runs build → unit → integration. Red pipeline =
   merge button locked until they push a fix.
2. Dev pushes more commits → pipeline re-runs automatically (old run auto-cancels).
3. Someone else merges to main → this MR's next run tests against the new main
   (auto-triggered on Premium; manual "Run pipeline"/rebase on Free).
4. MR merges → `main` pipeline runs the full suite again on the merged result.
5. Main is green → `deploy` becomes a click-to-run manual job. Main is red →
   deploy is unreachable.
