# AGENTS.md — Coding Agent (`src/`)

Governs production code in `src/`. Inherits the root `AGENTS.md`; adds the coding-specific rules below.
This agent **writes production code and the unit tests that drive it** (unit tests live in
`tests/MarqSpec.AgentForge.UnitTests`, but authoring them is part of *this* role — see TDD below). This agent
does **not** write integration tests (that is the Integration Testing Agent — `tests/AGENTS.md`).

## Mandatory workflow: test-first (TDD / BDD)
**Write the failing unit test before the implementation. No exceptions.** Red → Green → Refactor:
1. **Red.** Author the failing unit test(s) first, derived from the *contract* — the `USERS.md` use case, the
   tool input/output schema (ICD / NFR-CONTRACT-1), and the relevant FR/NFR — **not** from code that doesn't
   exist yet. Express behavior as BDD **Given/When/Then**, surfaced via `Method_State_ExpectedResult` naming.
   Run and confirm they fail for the right reason.
2. **Green.** Write the **minimum** production code to pass. No production behavior exists until a failing test
   required it.
3. **Refactor** with tests green.

Checkable expectations:
- **No new public method without a test written first** that failed before the method existed.
- Tests specify **behavior, not implementation** — never assert private details; a test that still passes when
  the behavior is wrong is not a spec.
- **Author the test file before the implementation file** in your change set (order visible in history).
- **Bug fixes are regression-first:** reproduce with a failing test, then fix.

## Unit tests you write here
- **Fully mocked** with **FakeItEasy** — fake *every* external dependency (`IOpenEmrFhirApi`, `ILlmProvider`,
  clock, config). **No network, DB, or file I/O.** Deterministic and fast.
- Cover all public methods including edge/error paths (timeouts, 4xx/5xx, malformed FHIR, empty bundles,
  cancellation, Polly-exhaustion → degrade). Assertions with **FluentAssertions** (`[6.12.0,8.0.0)`).
- Each test guards a **named failure mode** (boundary / invariant / regression — `PRD.md` FR-EVAL-1).

## Production coding standards (full detail in `ENGINEERING_STANDARDS.md` §3–§7)
- **DI through the constructor**; no static service locators; no global/ambient state.
- **Immutability by default** (`record`/`readonly`, `required`, `init`); `sealed` by default.
- **Refit** for external HTTP clients; cross-cutting concerns (auth token, correlation id) in
  `DelegatingHandler`s, not call sites.
- **Polly** resilience on every external call (timeout, retry+jittered backoff on transient only, circuit
  breaker); on exhaustion **degrade deterministically** (source-cited data, no synthesis) — never fabricate,
  never fail silently (`ARCHITECTURE.md` §13.1).
- **Options pattern** for config with `ValidateOnStart`. **`ILogger`** structured logging, correlation-scoped,
  no PHI. **`async` + `CancellationToken`** on all I/O.
- **`System.Text.Json`** source-generated contexts; exhaustive `switch` on enums.
- Authorization is enforced **below the model** (in the tool/data layer), never by prompt text.

## Definition of done
Failing unit tests written first and now green · standards met · no PHI anywhere · traces to a `USERS.md`
use case · tool/contract schemas updated if the interface changed · `dotnet format` clean.
