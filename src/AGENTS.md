# AGENTS.md — Coding Agent (`src/`)

Governs production code in `src/`. Inherits the root `AGENTS.md`; adds the coding-specific rules below.
This agent **writes production code and the unit tests that drive it** (unit tests live in
`tests/AgentForge.UnitTests`, but authoring them is part of *this* role — see TDD below). This agent
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

## Your PR is not done until someone else has ruled on it

**Open the PR, wait for it to go green, then start the Code Reviewer yourself and block on its verdict.**
Not because a rule says so: a review that arrives after your session ends lands in an empty room, and gets
addressed hours later by a session that has to rebuild your reasoning from the diff. You are the cheapest
reader this change will ever have.

```
gh pr create ...                                     # with Closes #N, per root AGENTS.md
# wait for the gates to go green
# start a Code Reviewer subagent, handing it ONLY the PR number
bash .github/scripts/watch-verdict.sh verdict <pr>   # blocks; 0 approve, 1 changes, 2 no ruling
```

**Starting the reviewer is not reviewing your own work**, and four things are what make that true:

- **Hand it the PR number and nothing else.** It resolves the base, the head and the diff itself. Anything
  you tell it about the change is a **claim of the same standing as the PR body** — something for it to
  verify, never something it may skip verifying because it came from inside the house.
- **It posts its own verdict.** You do not relay it. The PR is the durable record and what the gate reads;
  a ruling routed back through the reviewed party lets the reviewed decide what the review said.
- **You do not argue it into approving.** Fix the finding, push, and let it re-review — a push is what
  clears *Request changes*. If you believe a finding is wrong, say so **on the PR** where the disagreement
  is on the record, and let it rule again.
- **Exit 2 is not approval.** It means no ruling the gate can read — the reviewer never ruled, or ruled
  somewhere invisible (a PR comment and an inline comment both look right and count for nothing). Treat it
  as unreviewed and say so; do not merge around it.

An approval binds to what was reviewed, so a rebase or a target-sync keeps it while a new commit or a
conflict resolution kills it. Push a fix, expect to wait for a fresh ruling.

**On `Request changes`:** if you are running under the Coordinator, it dispatches the fixes — one work
item per blocking finding, spawned as a fresh Coding Agent with the PR number and the review body
(`documentation/agents/coordinator.md`). Standalone, you are that agent: fix, verify, push, and wait for
the re-review yourself. Either way the fix is made by a coder and the ruling by a reviewer, and the two
are never the same pass.

## Definition of done
Failing unit tests written first and now green · standards met · no PHI anywhere · traces to a `USERS.md`
use case · tool/contract schemas updated if the interface changed · **a Code Reviewer verdict on the PR, approved** (`watch-verdict.sh` exit 0 — exit 2 is unreviewed, not approved) · **`documentation/` checked for staleness and any stale doc updated in this same change** (root `AGENTS.md`) · `dotnet format` clean.
