# AGENTS.md — AgentForge Clinical Copilot (root)

Instructions for AI coding agents working in this repository. This file holds **only what is true for every
agent everywhere** plus the routing table below. Anything that belongs to one role lives in that role's own
contract — if you are about to add a rule here that starts "the reviewer should…", it belongs in
[`documentation/agents/`](documentation/agents/) instead.

## What this repo is
The **.NET sidecar** for an AI clinical copilot embedded in OpenEMR for the outpatient cardiologist. It
integrates with OpenEMR only over standard FHIR/OAuth/SMART. Base namespace: **`AgentForge`**;
solution: **`AgentForge.slnx`** (repo root), with projects under `src/` and `tests/`.

## Source of truth (read before coding)
**Start at `README.md`, then `documentation/INDEX.md` — the wiki's front door.** `INDEX.md` sequences the docs
below and carries the doc/requirement/use-case → code-project traceability leg the individual files don't;
traverse from there instead of re-deriving context each session. The `documentation/` folder is authoritative;
these AGENTS files summarize and point to it.
- `PRD.md` — requirements (FR-/NFR- IDs). `USERS.md` — the user + use cases every capability must trace to.
- `ARCHITECTURE.md` — decisions & topology. `ENGINEERING_STANDARDS.md` — stack, standards, testing tiers.
- `INTERFACE_CONTROL.md` — the OpenEMR external interface (ICD).

## Universal rules (all agents, everywhere)
- **.NET 10 (LTS), C# latest, `Nullable` on, warnings-as-errors.** File-scoped namespaces.
- **Synthetic/demo data ONLY — never real PHI**, anywhere: code, tests, fixtures, logs, telemetry.
- **No secrets in source.** Config/credentials/endpoints via the Options pattern + environment (see
  `ENGINEERING_STANDARDS.md` §6).
- **Structured logging via `ILogger`**, correlation ID on every request/call, **no PHI in logs** (§7).
- **Dependencies** via Central Package Management. Respect version caps — notably **FluentAssertions
  `[6.12.0,8.0.0)`** (v8+ is commercially licensed).
- **Every capability traces to a `USERS.md` use case.** If it doesn't, don't build it.
- **Contracts are the source of truth** (strict tool I/O schemas; NFR-CONTRACT-1). External calls conform to
  `INTERFACE_CONTROL.md`.
- **Comments are terse.** Inline comments are one short line, only for non-obvious *why* (a hidden constraint,
  a workaround, a surprising invariant) — never restate *what* the code does. XML doc comments on public
  methods describe behavior/contract only.
- **Reference comments:** Must be prefixed `reference:` (e.g. `// reference: documentation/ARCHITECTURE.md §9` or `// reference: labs.gauntletai.com#140`). A bare `#N`/`!N` and legacy `gitlab#N` citations both mean the **live GitLab tracker** (`labs.gauntletai.com`, project `1464`) and still resolve; `gh#N` means the **GitHub mirror**, which numbers its issues independently. Never cite a GitHub issue as a bare `#N`. See `documentation/INDEX.md` §5.
- **Commits:** Conventional Commits; add `Assisted-by:` trailer when authored by an AI agent.
- **No orphaned MRs:** Every merge request references a tracking issue (`Closes #N` / `Related to #N`) opened *before* it.

## Routing — find your contract, then open it

**Work out which hat you are wearing and open that contract before you start.** This table is the whole of what
this file says about roles; each contract owns its own rules.

| If you are… | Your contract | ~tok | Arrives |
|---|---|---|---|
| writing `src/` code or the unit tests driving it | [`src/AGENTS.md`](src/AGENTS.md) | 0.8K | on its own, when you read a file there |
| writing integration tests against real dependencies | [`tests/AGENTS.md`](tests/AGENTS.md) | 0.7K | on its own, in that project |
| reviewing a change — any change, anywhere | [`documentation/agents/code-reviewer.md`](documentation/agents/code-reviewer.md) | 2.1K | **never on its own — open it** |
| touching CI, the image, compose, the proxy or a deploy | [`documentation/agents/platform.md`](documentation/agents/platform.md) | 1.3K | **never on its own — open it** |
| shepherding an MR back to green — dispatching fixes, not writing them | [`documentation/agents/coordinator.md`](documentation/agents/coordinator.md) | 0.9K | **never on its own — open it** |

A contract sits wherever it has to be to arrive when it applies: subtree contracts load by directory proximity,
role contracts follow what you are *doing* and so cannot. **Nothing catches a hat worn without its contract** —
no check fails and no reviewer sees a diff. [`documentation/agents/README.md`](documentation/agents/README.md) is
the index, and explains the design and the tooling that narrows the gap.

## Build / test
- Build: `dotnet build AgentForge.slnx`
- Unit tests (fast, mocked): `dotnet test tests/AgentForge.UnitTests`
- Eval rubric tests (xUnit): `dotnet test tests/AgentForge.EvalTests`
- Eval console gate: `dotnet run --project tests/AgentForge.Evals -- evals`
- Integration tests (QA env, real deps): `dotnet test tests/AgentForge.IntegrationTests`
- Before opening an MR: `dotnet format --verify-no-changes` + unit & eval tests green. GitLab runs **only**
  the automated Code Reviewer (`documentation/CI-SETUP.md` §7); the build and test gates live on the GitHub
  mirror, so on a GitLab-only branch they are on you.
