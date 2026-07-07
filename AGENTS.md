# AGENTS.md — AgentForge Clinical Co-Pilot (root)

Instructions for AI coding agents working in this repository. **Nested `AGENTS.md` files take precedence for
their subtree:** `src/AGENTS.md` governs the **Coding Agent**; `tests/AGENTS.md` governs the **Integration
Testing Agent**. This root file holds the rules that apply everywhere.

## What this repo is
The **.NET sidecar** for an AI clinical copilot embedded in OpenEMR for the outpatient cardiologist. It
integrates with OpenEMR only over standard FHIR/OAuth/SMART. Base namespace: **`GauntletAI.AgentForge`**;
solution: **`GauntletAI.AgentForge.slnx`** (repo root), with projects under `src/` and `tests/`.

## Source of truth (read before coding)
The `documentation/` folder is authoritative; these AGENTS files summarize and point to it.
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
- **Commits:** Conventional Commits; add `Assisted-by:` trailer when authored by an AI agent.

## The two agent roles
| Agent | Scope | Definition |
|---|---|---|
| **Coding Agent** | `src/` production code **and** the test-first unit tests that drive it | `src/AGENTS.md` |
| **Integration Testing Agent** | the integration test project (real deps in QA) | `tests/AGENTS.md` |

## Build / test
- Build: `dotnet build GauntletAI.AgentForge.slnx`
- Unit tests (fast, mocked): `dotnet test tests/GauntletAI.AgentForge.UnitTests`
- Integration tests (QA env, real deps): `dotnet test tests/GauntletAI.AgentForge.IntegrationTests`
- Before opening a PR: `dotnet format --verify-no-changes` + unit tests green.
