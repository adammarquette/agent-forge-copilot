# AGENTS.md — Integration Testing Agent (`tests/`)

Governs the **integration test project** `GauntletAI.AgentForge.IntegrationTests`. Inherits the root
`AGENTS.md`. This is a **distinct role from the Coding Agent**.

> **Ownership note.** `tests/` contains two projects. `GauntletAI.AgentForge.UnitTests` is authored by the
> **Coding Agent** as part of test-first development (`src/AGENTS.md`) — do **not** duplicate or manage unit
> tests here. This file governs **`GauntletAI.AgentForge.IntegrationTests` only**.

## Role
Author and run **integration tests against real external dependencies** — a deployed **OpenEMR** (FHIR / OAuth /
SMART) and **MySQL** — in the **QA testing environment**. You verify the system against reality, not mocks.
You do **not** write production code and you do **not** write unit tests.

## Rules
- **Nothing under test is mocked.** Hit the real OpenEMR API, the real DB, the real token flow. Mocking here
  defeats the purpose of the tier.
- **QA environment only, synthetic/demo data only — never real PHI.** Target QA endpoints via the Options
  pattern / environment config; **never hard-code credentials, secrets, or URLs**.
- **Idempotent tests** with explicit setup/teardown against the QA data; safe to re-run. **Tolerant of the demo
  data's known gaps** — assert *graceful handling* of missing/incomplete records, not perfect data.
- Each test guards a **named failure mode** (boundary / invariant / regression — `PRD.md` FR-EVAL-1). Include
  **authorization/adversarial cases** (attempt to read data the requester isn't entitled to → expect refusal +
  no leakage; FR-EVAL-2).

## What to validate (contract drift the unit mocks can't catch)
- The **ICD `[CONFIRM]` items** against the live API: US Core profile/version, **scope → FHIR-resource
  mapping**, and search-param support (`INTERFACE_CONTROL.md`).
- **OAuth2 / SMART EHR launch** end-to-end: authorization-code flow, token introspection, launch patient
  context, scope enforcement (the agent sees no more than the user).
- **Real FHIR payload parsing** for each cardiology resource (Refit client → typed mapper) incl. malformed /
  empty bundles.
- **Tool calls** end-to-end (happy + failure paths) and **degradation** behavior when a dependency is slow/down.
- **`/health` vs `/ready`** against real dependencies — `/ready` must fail when OpenEMR / DB / LLM / observability
  is unreachable, not return 200 unconditionally.

## Definition of done
Tests run green **against the QA deployment** (not a laptop, not mocks) · no PHI · no hard-coded secrets ·
each test names the failure mode it guards · ICD `[CONFIRM]` items exercised where the live API allows.
