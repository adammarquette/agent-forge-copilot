# ENGINEERING STANDARDS — AgentForge Clinical Co-Pilot Sidecar

**Repo:** `agent-forge-copilot` (.NET sidecar)
**Companion docs:** `ARCHITECTURE.md` (decisions), `INTERFACE_CONTROL.md` (external interfaces), `PRD.md`,
`USERS.md`.
**Status:** v0.1. This is the "how we build" doc: runtime, dependencies (with rationale + license notes),
and the coding / testing / config / logging / resilience standards for the sidecar.

> **Source of truth for versions:** the `.csproj` files and a central `Directory.Packages.props`
> (Central Package Management). This document explains the *why* and the standards around each choice; it is
> not the canonical version list. When they disagree, the manifest wins and this doc is updated.

---

## 1. Runtime & Language

- **.NET 10 (LTS)** — long-term support (~3-year window), the right call for a long-lived healthcare service
  (avoids a forced runtime migration mid-pilot; see `ARCHITECTURE.md` D7).
- **C#** latest language version enabled (`<LangVersion>latest</LangVersion>`).
- **Nullable reference types ON** solution-wide (`<Nullable>enable</Nullable>`).
- **Warnings as errors** in CI (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`), plus
  `<AnalysisLevel>latest-recommended</AnalysisLevel>`.
- **Implicit usings** on; **file-scoped namespaces** required.

---

## 2. Dependency Manifest

Managed via **Central Package Management** (`Directory.Packages.props`) so versions are declared once for the
whole solution. Minimums below are floors; **note the caps** where a newer major version changes licensing or
compatibility.

| Package | Version constraint | Purpose | Notes |
|---|---|---|---|
| **Refit** | `>= 7.0.0` | Typed REST/HTTP clients — turns the OpenEMR FHIR/REST surface into C# interfaces (see ICD) | Contracts as interfaces; pairs with `Refit.HttpClientFactory` for DI |
| **Microsoft.AspNetCore.SignalR.Client** | `>= 9.0.0` | Real-time push to the iFrame SPA — streams the "fast core, then defer" answer (NFR-PERF-1) | 9.0.0+ is the .NET 10-compatible line |
| **Microsoft.Extensions.Http.Resilience** | `>= 9.0.0` | Transient-fault handling: retry w/ backoff, timeout, circuit breaker on external calls | The Polly-v8-based standard for `HttpClient` pipelines — the actual package pinned in `Directory.Packages.props` (no bare `Polly` reference); underpins failure-mode behavior (`ARCHITECTURE.md` §13.1 / `PRD.md` §13.1) |
| **OpenTelemetry** (+ `.Extensions.Hosting`, `.Instrumentation.AspNetCore`/`.Http`/`.Runtime`, `.Exporter.Prometheus.AspNetCore`, `.Exporter.Console`) | `>= 1.15.0` | Traces/metrics (latency, tool counts, tokens/cost, verification pass/fail) feeding the dashboard (FR-OBS-2/3, `ARCHITECTURE.md` §11) | Never a hard dependency on a specific backend (§7). `Exporter.Prometheus.AspNetCore` is pinned to a beta version deliberately — it has stayed in beta upstream for years (spec churn, not instability); it's the accepted, standard way to expose a `/metrics` scrape endpoint from ASP.NET Core and there is no stable release to pin to instead |
| **Microsoft.Extensions.Options** | current w/ .NET 10 | Strongly-typed configuration (`IOptions<T>`) for credentials/endpoints | Bind from env vars / appsettings; validate on start |
| **Microsoft.Extensions.Logging.Abstractions** | current w/ .NET 10 | `ILogger` abstraction throughout — no hard dependency on a provider | Consuming host picks Serilog/NLog/etc. (samples in §7) |
| **xUnit** | `>= 2.9.0` | Unit test framework | See testing standards (§8) |
| **FakeItEasy** | `>= 8.0.0` | Mocking/faking dependencies in tests | Clean OSS (MIT); chosen over Moq |
| **FluentAssertions** | `>= 6.12.0` **and `< 8.0.0`** | Readable assertions in tests | ⚠️ **License:** v8.0+ (Jan 2025) is **commercial** ($130/dev/yr for commercial use); v7.x and earlier remain **Apache-2.0 free**. Pin **`[6.12.0,8.0.0)`** so NuGet cannot silently resolve to the paid v8 |

**Rule:** every third-party package must have a stated purpose and a known license. Run a license check in CI
(e.g. `dotnet-project-licenses`) so a transitive bump into a restrictive license is caught, not discovered.

---

## 3. Coding Standards (C# / .NET)

Modern .NET idioms — do not carry over patterns from the OpenEMR PHP core.

- **Immutability by default.** `record` / `readonly record struct` for DTOs and value objects; `required`
  members over constructors-with-nulls; `init`-only setters. Mutable state is the exception.
- **`sealed` by default** on classes not designed for inheritance.
- **Dependency injection through the constructor.** No static service locators, no `new` on service-layer
  types inside business logic, no reaching into ambient/global state. Register everything in composition root.
- **Domain primitives** for values that PHP-style stringly-typing would confuse — e.g. `PatientId`,
  `EncounterId`, `Npi` — mirroring the trust-typing intent in `ARCHITECTURE.md`. Parse raw input into typed
  objects at the boundary (parse, don't validate).
- **`System.Text.Json`** with source-generated contexts for tool/DTO (de)serialization; strict schemas are the
  contract (NFR-CONTRACT-1).
- **Async all the way.** `async`/`await` end to end; **every** I/O method takes a `CancellationToken` and honors
  it (tool calls must be cancellable to hit the latency budget). No `.Result`/`.Wait()`.
- **Exhaustive `switch` expressions** on enums; avoid a `default` that silently swallows new cases.
- **Error handling:** catch the narrowest exception you can act on; never catch-and-swallow; never leak
  provider/PHI details into user-facing output (log with context, return generic). Let exceptions propagate
  when the caller can't recover.
- **No secrets in source or config files.** Credentials come from environment/secret store (§6).
- **No PHI in logs, exceptions surfaced to the client, or telemetry** (NFR-SEC-1).
- **Thread-safe & concurrency-ready.** The API client and shared services are safe for **concurrent API calls**
  — stateless where possible, typed HTTP clients via factory, **no shared mutable state without
  synchronization**; no cross-request interference.
- **Designed for extensibility.** Adding a new endpoint/feature is **additive** — a new Refit method/interface +
  tool, not a rewrite. Program to interfaces; keep the tool surface open for extension.
- **Meaningful, safe errors.** On failure, return a **typed, meaningful** error/result that tells the caller
  *what category* failed (auth, not-found, transient, validation) — **never a raw exception, secret, PHI, or
  internal detail** (see §11 Security, §7 Logging).

---

## 4. HTTP Client Standards (Refit)

External APIs are declared as **Refit interfaces** (one per external system; see ICD for OpenEMR), registered
via `HttpClientFactory` so cross-cutting concerns live in `DelegatingHandler`s, not call sites:

- **AuthHandler** — attaches the clinician's bearer token (from the BFF token store; never hard-coded).
- **CorrelationIdHandler** — propagates the correlation ID header on every outbound call (FR-OBS-1).
- **Resilience** — the Polly pipeline (§5) is attached to the named client, not sprinkled through methods.
- **HTTPS enforced** — REST calls use `https://` only; non-HTTPS base addresses are rejected. **Server
  certificate validation stays ON** (no `ServerCertificateCustomValidationCallback` bypass) outside an isolated,
  explicitly-flagged local path — never in QA/prod (see §11).

```csharp
public interface IOpenEmrFhirApi
{
    [Get("/apis/{site}/fhir/MedicationRequest")]
    Task<string> GetMedicationRequestsAsync(string site, [AliasAs("patient")] string patientId,
        CancellationToken ct = default); // returns FHIR JSON bundle; parsed by a typed FHIR mapper
}
```

---

## 5. Resilience (Polly)

Every outbound call to OpenEMR and the LLM provider goes through a **named resilience pipeline**:

- **Timeout** per attempt (aligned to the interactive latency budget — fail fast, don't hang the 90-sec window).
- **Retry** only on transient faults (5xx, 408, timeouts, transient network) with **exponential backoff +
  jitter**; never retry non-idempotent or auth failures.
- **Circuit breaker** to shed load when a dependency is down (feeds `/ready` and the degradation path).
- **Rate limiting (HTTP 429):** automatic retry with **exponential backoff + jitter**, honoring a `Retry-After`
  header when present; cap attempts, then degrade.
- On exhaustion, **degrade deterministically** per `ARCHITECTURE.md` §13.1 (source-cited data, no synthesis) —
  never fabricate, never silent.

Prefer `Microsoft.Extensions.Http.Resilience` (the Polly-v8-based standard) for `HttpClient` pipelines.

---

## 6. Configuration (Options Pattern)

- Strongly-typed options bound from configuration: `services.AddOptions<OpenEmrOptions>().Bind(...)
  .ValidateDataAnnotations().ValidateOnStart();` — misconfiguration fails **at startup**, not first request.
- **Credentials and endpoints** (OpenEMR base URL, `site`, OAuth client id, LLM keys) come from **environment
  variables or a secret store**, layered over `appsettings.{Environment}.json`. Secrets never live in source.
- Use `IOptionsSnapshot<T>` where per-request refresh matters; `IOptionsMonitor<T>` for change notifications.
- **Sensitive configuration is encrypted at rest** — platform secret store / KMS or an encrypted config
  provider; never plaintext secrets in source, `appsettings`, images, or backups (§11).
- Per-environment config maps to the deployment split in `ARCHITECTURE.md` §13 (Railway dev / AWS prod).

---

## 7. Logging & Observability

- **`ILogger` abstraction everywhere** (`Microsoft.Extensions.Logging.Abstractions`) — the sidecar never binds
  to a concrete provider; the host chooses.
- **Structured logging only.** Use message templates with named properties — **never string-interpolate**
  variables into the message, and **never** log PHI:

  ```csharp
  // BAD:  logger.LogInformation($"Fetched labs for {patientId}");   // PHI + unstructured
  // GOOD: logger.LogInformation("Fetched labs {ResourceCount} for correlation {CorrelationId}",
  //                             count, correlationId);
  ```

- **Correlation ID** flows as a logging scope on every request and every downstream call (FR-OBS-1).
- **OpenTelemetry** for traces/metrics (latency, tool counts, tokens/cost, verification pass/fail) feeding the
  dashboard (FR-OBS-2/3).
- **Logs also go through OpenTelemetry.** The OTel logging provider is wired with a console exporter always,
  plus an **OTLP/HTTP exporter to a self-hosted Loki** when `Observability:LokiOtlpEndpoint` is set (Epic 107).
  This keeps the provider-agnostic posture — Loki is one *optional, fail-open* backend choice, not a hard
  dependency: unset endpoint ⇒ console-only, and an unreachable endpoint never blocks the request path. Logs
  are searchable in Grafana next to the metrics. reference: `documentation/DEPLOYMENT_TOPOLOGY.md`, gitlab#107
- **Sample provider configs** (host-side; the sidecar itself stays provider-agnostic):
  - *Serilog:* `builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext().WriteTo.Console(new CompactJsonFormatter()));`
  - *NLog:* `builder.Logging.ClearProviders(); builder.Host.UseNLog();` with a JSON-target `nlog.config`.
  - Both must **redact/omit PHI** and include the correlation id in every record.

---

## 8. Testing Standards

Two test tiers, **two test projects** — no more. Framework: **xUnit** (`>= 2.9.0`); assertions:
**FluentAssertions** (`[6.12.0,8.0.0)`); mocking (unit tier only): **FakeItEasy** (`>= 8.0.0`).

Shared rules (both tiers): **Arrange-Act-Assert**; descriptive names (`Method_State_ExpectedResult`); one
behavior per test; **synthetic data only, never real PHI** (policy, not just convention); each test guards a
**named failure mode** — a boundary (missing data, malformed input, empty record), an invariant (a claim must
cite a source), or a regression risk (`PRD.md` FR-EVAL-1). Happy-path-only suites do not pass review.

### 8.0 Test-first (TDD/BDD) — mandatory workflow

**Unit tests are written before the implementation. This is not optional.** Any agent (or human) producing
code for this repo follows red-green-refactor:

1. **Red — write the failing unit test(s) first.** Derive them from the *contract*, not from code that doesn't
   exist yet: the `USERS.md` use case, the tool input/output schema (NFR-CONTRACT-1 / ICD), and the relevant
   FR/NFR. Express each as a behavior spec (BDD **Given/When/Then**, surfaced through the
   `Method_State_ExpectedResult` naming). Run them and **confirm they fail** for the right reason.
2. **Green — write the minimum production code** to make those tests pass. No production behavior exists until
   a failing test required it.
3. **Refactor — clean up with the tests green.** Behavior is pinned by the tests throughout.

Rules that make this checkable:
- **No new public method ships without a test that was written first** and that fails before the method exists.
  Every public method therefore has tests by construction (this is how the §8.1 coverage bar is met).
- **Tests specify behavior, not implementation.** They must not assert private details or be reverse-engineered
  from the code — if a test would still pass after the behavior is wrong, it isn't a spec.
- **Order is visible in history.** Commits/PRs should show tests landing before (or with) the implementation
  they drive — a coding agent should author the test file first, then the implementation file.
- **Applies to the unit tier (§8.1).** Integration tests (§8.2) follow the same spirit where feasible — define
  the expected external contract up front — but the strict test-first gate is enforced on unit tests.
- **When a bug is found, reproduce it with a failing test first,** then fix (regression-first).

### 8.1 Unit tests — `MarqSpec.AgentForge.UnitTests`
- **Fully mocked.** **FakeItEasy** fakes *every* external dependency — `IOpenEmrFhirApi`, `ILlmProvider`,
  clock, config, etc. **No network, no database, no file I/O.** The unit tier tests **expectations and behavior
  in isolation**, deterministically.
- Fast enough to run on **every build / PR**.
- **Coverage bar:** all **public methods** are exercised, including **edge and error scenarios** driven by
  faked failures — timeouts, 4xx/5xx, malformed FHIR, empty bundles, cancellation, Polly-exhaustion → degrade.
- **FluentAssertions** for readable assertions.

```csharp
[Fact] // unit tier: dependency faked, no real I/O
public async Task GetLabs_WhenFhirReturns500_RetriesThenDegrades()
{
    var api = A.Fake<IOpenEmrFhirApi>();
    A.CallTo(() => api.GetLabsAsync(A<string>._, A<string>._, A<CancellationToken>._))
     .Throws(new ApiException(/* 500 */));

    var result = await _sut.GetLabsAsync("default", "patient-1", CancellationToken.None);

    result.Degraded.Should().BeTrue();
    result.Citations.Should().NotBeEmpty("degraded output must still be source-cited");
}
```

### 8.2 Integration tests — `MarqSpec.AgentForge.IntegrationTests`
- **Run against real external dependencies** — a deployed **OpenEMR** (FHIR / OAuth / SMART), **MySQL**, and any
  other real service — in the **QA testing environment** (not a developer laptop, not mocks).
- **Nothing under test is mocked** — that is the point of this tier. These exercise the real **Refit** clients,
  the **OAuth2/SMART token flow**, actual **FHIR payload parsing**, and **end-to-end tool calls**.
- **Synthetic/demo data only** in the QA/SDET environment — never real PHI. Tests are **idempotent** and
  tolerant of the demo data's known gaps (they assert graceful handling, not perfect data).
- These are the tests that catch **contract drift the mocks cannot** — the ICD `[CONFIRM]` items (US Core
  profile version, scope→resource mapping, search-param support) are validated here against the live API.
- Slower and environment-dependent: run in CI **against the QA deployment** (and/or on a schedule), gated
  separately from the fast unit tier.

---

## 9. Solution Layout

**Base namespace / assembly prefix: `MarqSpec.AgentForge`.** The **solution sits at the repo root**
(`MarqSpec.AgentForge.slnx`) with `src/` and `tests/` as siblings — the conventional .NET layout. **Exactly
two test projects** — one unit, one integration (§8).

```
MarqSpec.AgentForge.slnx                   // solution (repo root)
src/
  MarqSpec.AgentForge.Api/                 // ASP.NET Core host: BFF, SignalR hub, /health + /ready
  MarqSpec.AgentForge.Agent/               // orchestrator: multi-turn loop, tool chaining
  MarqSpec.AgentForge.Mcp/                 // MCP tool server: contracts, audit log, read-only FHIR tools
  MarqSpec.AgentForge.Verification/        // source attribution + cardiology domain-constraint rules
  MarqSpec.AgentForge.Integration.OpenEmr/ // Refit clients, OAuth/SMART, FHIR mappers (see ICD)
  MarqSpec.AgentForge.Llm/                 // ILlmProvider abstraction + one implementation
  MarqSpec.AgentForge.Observability/       // OTel activity source + metrics (FR-OBS-2/3)
  MarqSpec.AgentForge.Data/                // (Week 2) EF Core + pgvector: entities, DbContext, migrations
                                             //   for the hybrid-RAG corpus + DerivedFactStore (W2_ARCHITECTURE.md §5, W2-D14)
tests/
  MarqSpec.AgentForge.UnitTests/           // §8.1 — fully mocked (FakeItEasy); expectations only; no I/O
  MarqSpec.AgentForge.IntegrationTests/    // §8.2 — real OpenEMR/MySQL in the QA environment; synthetic data only
```

Notes:
- The MCP tool server is its own project (`MarqSpec.AgentForge.Mcp`), consumed by the host
  (`MarqSpec.AgentForge.Api`). (Name the host as you prefer — `.Api` is the placeholder; the earlier
  `.Copilot` template project is removed.)
- The two test projects stay fixed regardless of how many `src/` projects exist — unit tests reference the
  projects they fake; integration tests reference the host.

---

## 10. CI Quality Gates
- `dotnet format --verify-no-changes` (style), analyzers at `latest-recommended`, warnings-as-errors.
- `dotnet test` with coverage threshold; the FR-EVAL boundary/invariant/regression suite runs here.
- License scan on the dependency graph (catches restrictive-license bumps — see FluentAssertions note §2).
- Contracts (tool schemas) exported and diffed (NFR-CONTRACT-1).
- Release version (`<Version>` / git tag) follows SemVer — see §14.
- **Every MR references a tracking issue** (`Closes #N` / `Related to #N`) stating the problem or requirement
  being addressed — no MR is merged without one. The issue is opened *before* the MR, not written up
  after the fact; it's where design decisions, scoping notes, and known gaps get recorded (root `AGENTS.md`).

---

---

## 11. Security Standards

Consolidated security requirements (also enforced in code via §4 HTTP, §6 Config, §7 Logging, §12 SignalR):

- **Secrets never logged or in exceptions.** API keys, tokens, client secrets, and credentials must **never**
  appear in log entries, **exception messages**, stack traces, telemetry, or error responses returned to a
  caller. Redact at the boundary; log a reference/lookup id, not the secret.
- **HTTPS for all REST.** Enforced; non-HTTPS endpoints rejected (§4).
- **WSS for all WebSocket connections.** All SignalR/WebSocket traffic uses `wss://` only — no plaintext
  `ws://` (§12).
- **SSL/TLS certificate validation ON by default.** The client validates server certificates; disabling or
  bypassing certificate validation is prohibited (except an isolated, explicitly-flagged local dev path — never
  in QA/prod).
- **Encryption at rest for sensitive configuration.** Secrets/credentials at rest are encrypted — via the
  platform secret store (cloud KMS / secret manager) or an encrypted configuration provider; **never plaintext
  secrets** in source, `appsettings`, container images, or backups.
- **Secure authentication handling.** Tokens are held **server-side (BFF)**, attached via a `DelegatingHandler`,
  and never exposed to the browser, logs, or error messages (`ARCHITECTURE.md` D11).
- **PHI:** synthetic/demo data only; never in logs or telemetry (root `AGENTS.md`, §7).

## 12. Real-time / WebSocket (SignalR) Standards

- **WSS only.** All hub connections use secure WebSockets (`wss://`); plaintext `ws://` is prohibited (§11).
- **Reliable delivery — no silent drops.** Failed outbound messages are **queued and retried** (bounded, with
  backoff); if still undeliverable, they are **reported to observers** (surfaced on an error/event channel and
  logged with the correlation id), never silently discarded (aligns with "never fail silently",
  `ARCHITECTURE.md` §13.1).
- **Automatic reconnect** with backoff; on reconnect, resume/replay queued messages **idempotently**.
- **Thread-safe client.** The hub client is safe for concurrent publishes; no shared mutable state without
  synchronization.
- Primary use: streaming the "fast core, then defer" answer to the iFrame (NFR-PERF-1).

## 13. API Documentation Standards

- **XML documentation comments on all public types, models, and methods.** Enable
  `<GenerateDocumentationFile>true</GenerateDocumentationFile>`; treat missing-doc warnings (CS1591) as errors
  on the public surface.
- **Match the source contract.** Doc comments for OpenEMR-facing models/methods **match the descriptions in the
  OpenEMR OpenAPI/Swagger** definitions (and the ICD) — same wording where practical, so the generated reference
  and the upstream API agree.
- **Usage examples + API reference.** Ship concise usage examples (README/docs) and a generated API reference;
  include the sample logging-provider configs (§7) and Options/config examples (§6).

---

## 14. Versioning (SemVer)

**All versioning in this repo — the sidecar release, git tags, and tool/contract schemas — follows
[Semantic Versioning 2.0.0](https://semver.org/) (`MAJOR.MINOR.PATCH`), effective now.**

- **Release version.** The sidecar's version is tracked via `<Version>` in `Directory.Build.props`
  (solution-wide, one version for the whole sidecar). Starting point: `0.1.0`, matching the docs'
  existing "v0.1" status framing.
- **Bumps ride the existing commit convention — no separate versioning ceremony.** The root `AGENTS.md`
  already mandates Conventional Commits; the commit type *is* the version-bump signal:
  - `fix:` → **PATCH**
  - `feat:` → **MINOR**
  - a `!` after the type (e.g. `feat!:`) or a `BREAKING CHANGE:` footer → **MAJOR**
- **Pre-1.0 caveat.** While the sidecar is `0.x`, SemVer permits breaking changes on a MINOR bump — the
  API/contract surface isn't yet a stable public commitment. **`1.0.0` is a deliberate milestone** (a
  stable tool/contract surface worth committing to), not an incidental crossing.
- **Tags.** Releases are tagged `vMAJOR.MINOR.PATCH` on `main` at deploy time, so a version is always
  traceable to a commit and a deployed build.
- **Contract schemas force the issue.** The MCP tool input/output schemas (NFR-CONTRACT-1,
  `INTERFACE_CONTROL.md`) are the sidecar's public contract. A breaking schema change is a breaking
  change to the sidecar — it forces a MAJOR bump on its own, independent of how much application code
  actually changed.
- **Dependency versions already follow this discipline** — §2's floor/cap constraints (e.g. FluentAssertions
  `[6.12.0,8.0.0)`) are SemVer ranges; this section extends the same discipline to what this repo *ships*,
  not just what it depends on.

---

## 15. Agent Instructions & Roles (`AGENTS.md` / `CLAUDE.md`)

AI agents that build this repo are governed by **`AGENTS.md`** files (the cross-tool standard). They are laid
out as a hierarchy, and **the nearest file to what's being edited takes precedence / adds context**:

| File | Applies to | Role |
|---|---|---|
| `/AGENTS.md` | whole repo | Universal rules (runtime, no-PHI, secrets, logging, dependency caps, "trace to a use case") |
| `/src/AGENTS.md` | `src/` | **Coding Agent** — production code **and** the test-first unit tests that drive it |
| `/tests/AGENTS.md` | `tests/` | **Integration Testing Agent** — integration tests against real OpenEMR/MySQL in **QA** |

**Two distinct roles, by design:**
- **Coding Agent** (`src/`): implements the sidecar under **mandatory test-first TDD** (§8.0) — writes the
  failing unit test first, then the minimum code to pass. Owns `src/` and the `UnitTests` project.
- **Integration Testing Agent** (`tests/`): authors and runs the `IntegrationTests` project against real
  dependencies in the QA environment (nothing mocked, synthetic data only). Does not write production code or
  unit tests.

**Claude Code bridge:** Claude Code reads `CLAUDE.md`, not `AGENTS.md`, so each level carries a one-line
`CLAUDE.md` shim that imports its sibling `AGENTS.md` (`@AGENTS.md`). Both toolchains therefore honor the same
single source, with no duplicated content to drift.

**Authority:** the `AGENTS.md` files are intentionally short and **point to the `documentation/` docs as the
source of truth** — this file (`ENGINEERING_STANDARDS.md`) for stack/standards/testing, `INTERFACE_CONTROL.md`
for external interfaces, `ARCHITECTURE.md` for decisions, `USERS.md`/`PRD.md` for what to build. When an
`AGENTS.md` and a doc disagree, the doc wins and the `AGENTS.md` is corrected.

---

*v0.1 — pairs with `INTERFACE_CONTROL.md` (external interfaces) and `ARCHITECTURE.md` (decisions).*
