# AGENTS.md — AgentForge Clinical Co-Pilot (root)

Instructions for AI coding agents working in this repository. **Nested `AGENTS.md` files take precedence for
their subtree:** `src/AGENTS.md` governs the **Coding Agent**; `tests/AGENTS.md` governs the **Integration
Testing Agent**. This root file holds the rules that apply everywhere.

## What this repo is
The **.NET sidecar** for an AI clinical copilot embedded in OpenEMR for the outpatient cardiologist. It
integrates with OpenEMR only over standard FHIR/OAuth/SMART. Base namespace: **`MarqSpec.AgentForge`**;
solution: **`MarqSpec.AgentForge.slnx`** (repo root), with projects under `src/` and `tests/`.

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
- **Reference comments are allowed, but must be prefixed `reference:`.** A comment may point to a
  `documentation/` file/section or a GitLab issue/MR/epic (e.g., `// reference: documentation/ARCHITECTURE.md
  §9` or `// reference: gitlab#62`) when it's the fastest way to point a future agent at fuller context. The
  `reference:` prefix keeps these grep-able and visually distinct from an ordinary comment — it never
  substitutes for the *why*, which must still be stated inline, not left implicit behind the link.
- **Commits:** Conventional Commits; add `Assisted-by:` trailer when authored by an AI agent.
- **No orphaned MRs.** Every MR references a tracking issue (`Closes #N` / `Related to #N`) that states the
  problem or requirement being addressed, opened *before* the MR. If no issue exists yet for the work, open
  one first — don't retrofit one after the fact.

## The two agent roles
| Agent | Scope | Definition |
|---|---|---|
| **Coding Agent** | `src/` production code **and** the test-first unit tests that drive it | `src/AGENTS.md` |
| **Integration Testing Agent** | the integration test project (real deps in QA) | `tests/AGENTS.md` |

## Build / test
- Build: `dotnet build MarqSpec.AgentForge.slnx`
- Unit tests (fast, mocked): `dotnet test tests/MarqSpec.AgentForge.UnitTests`
- Integration tests (QA env, real deps): `dotnet test tests/MarqSpec.AgentForge.IntegrationTests`
- Before opening a PR: `dotnet format --verify-no-changes` + unit tests green.
