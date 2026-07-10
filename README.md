# AgentForge Clinical Co-Pilot — Sidecar

A .NET sidecar service that embeds an **AI clinical co-pilot into OpenEMR** for the **outpatient cardiologist**.
It gives a physician the context they need in the ~90 seconds between patient rooms — *what changed since the
last visit and what matters today* — as a **grounded, source-cited, conversational agent**, not a chatbot and
not a dashboard.

> **Status:** early / scaffolding. The **`documentation/` folder is the current source of truth**; the .NET
> projects are being built out under `src/` and `tests/`. This repo is the AI layer only — it integrates with a
> separate **OpenEMR fork** over standard FHIR/OAuth/SMART (see [Related repositories](#related-repositories)).
>
> **Data policy:** **synthetic / demo data only — never real PHI**, anywhere (code, tests, logs, fixtures).

---

## What this is (in one minute)

- **The user:** one narrow persona — an outpatient cardiologist mid-clinic. Every capability traces to a use
  case in [`USERS.md`](documentation/USERS.md).
- **The shape:** a **sidecar, not a fork** — the OpenEMR core is untouched; the sidecar reads PHI only through
  OpenEMR's FHIR R4 API using the **clinician's own OAuth identity** (SMART EHR launch), so it can never see
  more than that user could.
- **The trust core:** every response passes **two-layer verification** — (1) source attribution (each claim
  resolves to a real FHIR resource) and (2) cardiology domain-constraint rules (INR range, QT combos, renal
  dosing, etc.).
- **The stack:** C# / **.NET 10 (LTS)**, Refit (typed OpenEMR clients), SignalR (streaming to the UI), Polly
  (resilience), MCP tools, one LLM provider behind an `ILlmProvider` seam.

For the full picture, read the docs below in order.

---

## Live demo

A running OpenEMR instance (synthetic data only) is deployed on Railway — see [Deployment](#deployment):

**[openemr-uubp-development.up.railway.app](https://openemr-uubp-development.up.railway.app)**

| | |
|---|---|
| Username | `admin` |
| Password | `P@ssw0rd1` |

---

## Where to find things

| Path | What's there |
|---|---|
| [`documentation/`](documentation/) | All specs & design docs (the substance today — see index below) |
| `GauntletAI.AgentForge.slnx` | Solution file (repo root) |
| `src/` | Production projects (`GauntletAI.AgentForge.*`) — see layout in `ENGINEERING_STANDARDS.md` §9 |
| `tests/` | Two test projects: `…UnitTests` (mocked) and `…IntegrationTests` (real deps in QA) |
| `AGENTS.md` · `src/AGENTS.md` · `tests/AGENTS.md` | Instructions for AI coding agents (root + per-role) |
| `CLAUDE.md` (each level) | One-line shims so Claude Code honors the same `AGENTS.md` rules |

### Documentation index (suggested read order)

| Doc | Purpose |
|---|---|
| [`PRD.md`](documentation/PRD.md) | Product requirements — the problem, functional & non-functional requirements (FR/NFR IDs) |
| [`USERS.md`](documentation/USERS.md) | The target user, the 90-second workflow, and the use cases everything traces to |
| [`AUDIT.md`](documentation/AUDIT.md) | Findings from auditing the OpenEMR fork (security / perf / data quality) |
| [`ARCHITECTURE.md`](documentation/ARCHITECTURE.md) | The design & decision log — topology, trust boundaries, verification, deployment |
| [`INTERFACE_CONTROL.md`](documentation/INTERFACE_CONTROL.md) | Interface Control Document (ICD) — the OpenEMR external interface (FHIR/OAuth/SMART) |
| [`ENGINEERING_STANDARDS.md`](documentation/ENGINEERING_STANDARDS.md) | Stack, dependencies, coding/testing/security/logging standards |
| `Architecture_Defense.pptx` | 5-minute architecture-defense deck (summary of the above) |

---

## Architecture at a glance

```mermaid
flowchart LR
    MD([Cardiologist])

    subgraph OpenEMR["OpenEMR fork - core unmodified"]
        SMART[SMART EHR launch + OAuth2]
        FHIR[FHIR R4 API - US Core]
        AUDIT[(EventAuditLogger)]
    end

    subgraph Sidecar["This repo - .NET sidecar"]
        BFF[BFF - server-side token custody]
        ORCH[Agent orchestrator - multi-turn, tool chaining]
        MCP[MCP tool server - read-only FHIR, min-necessary]
        VERIFY[Verification - source attribution + cardiology rules]
    end

    LLM[[LLM provider - BAA assumed]]

    MD -->|SMART launch| SMART
    MD -->|chat| BFF
    BFF --> ORCH
    ORCH <--> MCP
    ORCH --> VERIFY
    MCP -->|clinician-scoped token| FHIR
    SMART -.-> FHIR
    FHIR -.-> AUDIT
    ORCH <-->|ILlmProvider| LLM
    VERIFY --> BFF
    BFF -->|cited answer| MD
```

Details and rationale: [`ARCHITECTURE.md`](documentation/ARCHITECTURE.md). External contract:
[`INTERFACE_CONTROL.md`](documentation/INTERFACE_CONTROL.md).

---

## Getting started

Prerequisites: **.NET 10 SDK** (or later). Build and test from the repo root:

```bash
dotnet build GauntletAI.AgentForge.slnx

# Unit tests — fully mocked, fast, no external deps
dotnet test tests/GauntletAI.AgentForge.UnitTests

# Integration tests — run against real OpenEMR/MySQL in the QA environment (see ENGINEERING_STANDARDS.md §8.2)
dotnet test tests/GauntletAI.AgentForge.IntegrationTests
```

Configuration (OpenEMR base URL / site, OAuth client, LLM keys) is supplied via the **Options pattern** —
environment variables layered over `appsettings.{Environment}.json`. **No secrets in source**; sensitive config
is encrypted at rest (`ENGINEERING_STANDARDS.md` §6, §11).

---

## How we work

- **Test-first (TDD/BDD) is mandatory.** Unit tests are written *before* the implementation (red → green →
  refactor). See `ENGINEERING_STANDARDS.md` §8.0 and `src/AGENTS.md`.
- **Two agent roles.** The **Coding Agent** (`src/`) writes production code + its test-first unit tests; the
  **Integration Testing Agent** (`tests/`) writes integration tests that run against real dependencies in QA.
  Each role's rules live in the nearest `AGENTS.md`.
- **Standards & contracts are authoritative.** `ENGINEERING_STANDARDS.md` governs how we build;
  `INTERFACE_CONTROL.md` governs the OpenEMR boundary; strict tool schemas are the source of truth.
- **Commits:** Conventional Commits; add an `Assisted-by:` trailer for AI-authored changes.

---

## Deployment

- **Dev / demo:** Railway (synthetic data only → no BAA required).
- **Production:** a HIPAA-eligible cloud under a signed BAA (default **AWS**, free self-serve via Artifact);
  the sidecar is also portable into a practice's own OpenEMR environment. See `ARCHITECTURE.md` §13.

---

## Roadmap (high level)

- **vMVP** (the first release): the conversational, cardiology-only co-pilot — interval-change brief + grounded follow-up, with
  verification, authorization, observability, and an eval suite.
- **Phase 2 (post-vMVP):** "Morning Triage" pre-clinic batch (pre-computes the panel; reuses the vMVP pipeline) —
  `ARCHITECTURE.md` §18.

---

## Related repositories

- **OpenEMR fork — [`agent-forge`](https://labs.gauntletai.com/adammarquette/agent-forge)** — the audited EHR
  base + the thin custom module that iFrame-launches this sidecar. The two repos are **highly coupled and
  sometimes need coordinated deploys**: this sidecar is a first-class dependency of `agent-forge`, not merely a
  system it happens to integrate with over a published interface — the fork's own module/shim (embedding the
  chat SPA, `ARCHITECTURE.md` §9's request flow) depends on this sidecar being deployed and working, so a
  sidecar-side change or outage can break `agent-forge` itself. Everything still crosses the boundary only
  through published FHIR/OAuth/SMART interfaces (no direct DB access, no shared code) — but when diagnosing a
  fork-specific quirk (auth flow, FHIR shape, scope handling), it's often faster to read the fork's actual PHP
  source (`src/RestControllers/AuthorizationController.php`, `SMARTAuthorizationController.php`,
  `TokenIntrospectionRestController.php`, etc. — see `INTERFACE_CONTROL.md` A's "Confirmed in fork" note) than
  to guess from HTTP responses/logs alone.

---

*Gauntlet AI — AgentForge Clinical Co-Pilot. Internal project; docs are living and versioned (`v0.1`).*
