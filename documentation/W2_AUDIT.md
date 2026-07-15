# W2_AUDIT.md — Week 2 implementation audit (Multimodal Evidence Agent)

**Audited:** the Week 2 implementation on `develop` against the requirements in [`W2_PRD.md`](W2_PRD.md)
(FR-/NFR-W2 IDs) and [`W2_ARCHITECTURE.md`](W2_ARCHITECTURE.md).
**Date:** 2026-07-15. **Method:** code inspection of `src/`, `tests/`, `evals/`, `.gitlab/ci/`, and the
deployed-config docs. **Scope:** implementation coverage, gaps, and risk — *not* a security pen-test or a
performance run.

> This audits *our sidecar's* Week 2 work. [`AUDIT.md`](AUDIT.md) is a separate audit of the OpenEMR fork.

**Legend:** ✅ Met · 🟡 Partial · ❌ Gap · Each finding cites the file(s) it's based on. "Not fully traced"
marks items asserted from a single reference rather than end-to-end verification.

---

## 1. Executive summary

The **trust spine and the graded HARD GATE are in place**: strict-schema extraction, the citation contract,
the supervisor + two-worker graph with logged handoffs, and a **50-case eval gate that blocks a regression**
(FR-EVAL-W2-*). The gaps cluster in three areas, and they line up exactly with the open Early-Submission work:

1. **RAG is sparse-only** — the "hybrid + rerank" half (dense pgvector + rerank) is not built (FR-RAG-2).
2. **The Week 2 observability surface is not instrumented** — the agent graph emits no Week 2 metrics and no
   trace spans; the dashboard has no Week 2 panels (FR-OBS-W2-1/2, NFR-TRACE-W2-1).
3. **Click-to-source overlay UI is absent** — the citation *data* (bounding boxes) exists, the UI does not
   (FR-CITE-2).

Plus a set of smaller "declared-but-thin" items: no committed OpenAPI spec / no W2 API collection coverage,
`/ready` doesn't check the vector index, no eval-regression alert, and no Week 2 performance baselines.

### Status at a glance

| Group | Status | One-line |
|---|---|---|
| FR-DOC — ingestion & extraction | ✅ Met | Schema-gated extraction, idempotent ingest, no write-back |
| FR-RAG — hybrid retrieval + rerank | 🟡 Partial | Sparse FTS only; **dense + rerank missing** |
| FR-GRAPH — supervisor + 2 workers | ✅ Met | Typed graph, handoffs logged (but not traced — see FR-OBS) |
| FR-CITE — citation contract + overlay | 🟡 Partial | Contract ✅; **bbox overlay UI missing** |
| FR-EVAL — golden set + PR gate | ✅ Met | 50 cases, 5 boolean rubrics, gate blocks a regression |
| FR-OBS — Week 2 telemetry + panels | ❌ Gap | **No W2 metrics emitted; no W2 panels** |
| NFR-CONTRACT-W2 | ✅ Met | Typed handoff/extraction contracts + contract tests |
| NFR-TRACE-W2 | ❌ Gap | Graph has no ActivitySource spans; workers not child spans |
| NFR-LOG-W2 | ✅ Met | W2 events extend the Week 1 structured-log schema |
| NFR-SLO-W2 | 🟡 Partial | 4 alert rules exist; **no eval-regression alert, no W2 SLO** |
| NFR-HEALTH-W2 | 🟡 Partial | `/ready` checks OpenEMR/LLM/Prometheus; **not the vector index** |
| NFR-CI-W2 | ✅ Met | evals gate + deterministic rubric tests in the pipeline |
| NFR-API-W2 | 🟡 Partial | Runtime OpenAPI only; **no committed spec, no W2 collection coverage** |
| NFR-TEST-W2 | ✅ Met | Hermetic fixture/stub tests; strategy documented |
| NFR-DATA/MIGRATE-W2 | ✅ Met | One-authority model + migration documented |
| NFR-SEC-W2 — PHI | 🟡 Partial | `no_phi_in_logs` rubric ✅; **DerivedFactStore at-rest encryption unverified** |
| NFR-BACKUP-W2 | 🟡 Partial | Documented; eval set repo-reproducible; recovery untested |
| NFR-PERF-W2 | ❌ Gap | No Week 2 baselines in PERFORMANCE_BASELINES.md (Should) |

---

## 2. Detailed findings

### 2.1 FR-DOC — Document ingestion & extraction — ✅ Met
- **FR-DOC-1/2 (ingestion tool + schema-as-gate):** `DocumentIngestionService`, `IngestionEndpoints`
  (`POST /documents/ingest`), and `DocumentExtractor` validate against strict schemas (`LabExtraction`,
  `IntakeExtraction`) and reject malformed output with a `RejectionReason` (`DocumentExtractionResult`). Raw
  VLM output does not bypass validation. ✅
- **FR-DOC-3 (write-back + authority + idempotency):** `ContentHash` + `IngestedDocument` give content-hash
  idempotency; OpenEMR stays authoritative, derived facts are sidecar-owned (`DerivedFactStore`), no
  Observation write-back — matches the E2 pivot. ✅
- **FR-DOC-4/5 (required fields + confidence):** extraction schemas carry the required fields and per-fact
  citations. *Not fully traced:* field-level **confidence** labeling (FR-DOC-5, a "Should") — confirm each
  fact emits a confidence/`null`+flag rather than a guess.

### 2.2 FR-RAG — Hybrid retrieval + rerank — 🟡 Partial (headline gap)
- **FR-RAG-1 (corpus):** `GuidelineCorpusSeeder` builds a committed guideline corpus. ✅
- **FR-RAG-2 (hybrid + rerank):** **only the sparse half exists** — `FtsEvidenceRetriever` (Postgres FTS). Its
  own doc comment names dense pgvector + RRF fusion + rerank as the "documented fast-follow." No embeddings,
  no reranker, no fusion. ❌ **This is the Early-Submission "hybrid RAG + rerank" deliverable.**
- **FR-RAG-3 (evidence vs record facts):** `CitationSourceType` separates `guideline` from `fhir`/`derived`. ✅

### 2.3 FR-GRAPH — Supervisor + two workers — ✅ Met
- `EvidenceAgentSupervisor` + `EvidenceAgentContracts` route over typed state to an intake-extractor and an
  evidence-retriever worker; handoffs are logged (`EvidenceAgentSupervisorLog`). Contract-tested
  (`EvidenceAgentSupervisorTests`). ✅
- **FR-GRAPH-3 (critic gate):** the graph references the Week 1 verification. *Not fully traced:* confirm an
  uncited/​unsafe claim in the **evidence-answer** path is actually suppressed (the Week 1 `SourceAttribution`
  engine keys on `[ResourceType/Id]` — verify it covers guideline citations too).
- ⚠️ The graph is **logged but not traced or metered** — see FR-OBS / NFR-TRACE.

### 2.4 FR-CITE — Citation contract + click-to-source — 🟡 Partial
- **FR-CITE-1 (machine-readable citation):** `Citation` carries `SourceType/SourceId/PageOrSection/
  FieldOrChunkId/QuoteOrValue` and a normalized `BoundingBox`. ✅
- **FR-CITE-2 (bbox overlay UI):** the *data* is there; the **UI is not** — no PDF preview / highlight overlay
  in `wwwroot/` (issue #96 open). ❌

### 2.5 FR-EVAL — Golden set + PR-blocking gate — ✅ Met (the graded HARD GATE)
- **FR-EVAL-W2-1:** exactly **50** golden cases (`evals/golden/*.json`). ✅
- **FR-EVAL-W2-2:** all five boolean rubrics present (`RubricEvaluator`): `schema_valid`, `citation_present`,
  `factually_consistent`, `safe_refusal`, `no_phi_in_logs`; deterministic ones also run as xUnit theories
  (`.gitlab/ci/test.yml`). ✅
- **FR-EVAL-W2-3:** the `evals` job (`.gitlab/ci/evals.yml`) fails the pipeline on a threshold/regression breach
  vs `evals/baseline.json` — verified by injecting a regression. ✅
- **FR-EVAL-W2-4:** refusal / missing-data / not-JSON cases are in the set. ✅
- ⚠️ **Coverage caveat:** the golden set (and `RubricEvaluator`, which scores a `DocumentExtractionResult`) is
  **extraction-centric**. There appear to be **no evidence-retrieval / RAG-grounding cases**, so the gate does
  not currently guard FR-RAG behavior. Worth a few added cases once FR-RAG-2 lands.

### 2.6 FR-OBS — Week 2 observability & cost — ❌ Gap
- **FR-OBS-W2-1 (per-encounter W2 telemetry):** `AgentForgeMetrics` publishes **Week 1 metrics only** —
  agent turns, tool calls, verification results, LLM tokens/cost. **Missing:** document-ingestion count/latency,
  extraction confidence, retrieval hit rate, reranker latency, supervisor routing decisions, per-worker latency,
  eval outcome. The `.Agents` project references **neither** `IAgentForgeMetrics` **nor** any ActivitySource. ❌
- **FR-OBS-W2-2 (W2 dashboard panels):** the Grafana dashboard has no Week 2 panels (nothing to show until the
  metrics above exist). The Prometheus+Grafana stack is now *deployed* to staging (#57), but the app-side
  instrumentation is the missing half. ❌
- **FR-OBS-W2-3 (cost & latency report):** not present (a Final deliverable). ❌ pending.

### 2.7 NFR findings
- **NFR-CONTRACT-W2 — ✅** typed handoff + canonical extraction schemas, contract-tested.
- **NFR-TRACE-W2 — ❌** no distributed tracing across the graph; workers are not child spans of a supervisor
  span (`ActivitySource` is used in `.Agent`/`.Mcp`, **not** in `.Agents`). Correlation-ID logging exists, but
  "reconstruct a full multi-agent trace from the correlation ID" is not met at the span level.
- **NFR-LOG-W2 — ✅** W2 log events extend the Week 1 schema (`*Log.cs` partial-class pattern).
- **NFR-SLO-W2 — 🟡** `observability/alerts/agentforge-alerts.yml` has 4 rules, but **no eval-regression
  alert** (specifically called out as demonstrable) and no ingestion/retrieval SLO.
- **NFR-HEALTH-W2 — 🟡** `/ready` aggregates `OpenEmrHealthCheck`, `LlmProviderHealthCheck`,
  `ObservabilityHealthCheck` (degraded-aware) — but **no vector-index (Postgres) readiness** and no reranker
  check (the reranker doesn't exist yet).
- **NFR-CI-W2 — ✅** evals gate + deterministic rubric theories in the pipeline. *Not fully traced:*
  dependency-audit + security-scan on every PR (confirm present in CI).
- **NFR-API-W2 — 🟡** runtime OpenAPI via `Microsoft.AspNetCore.OpenApi` exists, but **no committed OpenAPI
  spec**, no contract test asserting impl-vs-spec sync, and the Bruno collection covers only
  Chat/Health/Launch/Metrics — **not** the W2 endpoints (ingestion, evidence retrieval, full W2 flow).
- **NFR-TEST-W2 — ✅** hermetic fixture/stub eval tests run offline in CI; unit + BFF integration tests present.
- **NFR-DATA / MIGRATE-W2 — ✅** one-authority model + `InitialCreate` migration; documented in W2_ARCHITECTURE.
- **NFR-SEC-W2 — 🟡** `no_phi_in_logs` rubric *is* the CI PHI check (met). *Not fully traced:* the
  `DerivedFactStore` "encrypted at rest, clinician-scoped, audited" claim — confirm encryption is actually
  applied, not just intended.
- **NFR-BACKUP-W2 — 🟡** documented; the eval set is repo-reproducible (✅ that part); recovery procedure
  untested.
- **NFR-PERF-W2 — ❌** no Week 2 baseline profiles in `PERFORMANCE_BASELINES.md` (a "Should").

---

## 3. Key risks

| # | Risk | Why it matters |
|---|---|---|
| R1 | **"Hybrid RAG + rerank" advertised, sparse-only shipped** (FR-RAG-2) | Core Req 3 + the Early-Submission line item; the term "hybrid" is a grading checkpoint |
| R2 | **Eval gate doesn't cover RAG** (FR-EVAL coverage) | The HARD GATE guards extraction but not evidence grounding — a RAG regression wouldn't turn CI red |
| R3 | **W2 graph is a black box to telemetry** (FR-OBS-W2-1, NFR-TRACE-W2) | No per-worker latency/spans/metrics → can't show the graph is healthy or produce the cost/latency report from real data |
| R4 | **DerivedFactStore at-rest encryption unverified** (NFR-SEC-W2) | New PHI-at-rest surface (RW3); "intended" ≠ "applied" |
| R5 | **No committed API spec / W2 collection** (NFR-API-W2) | A grader can't run the W2 workflows from a collection without reading source |

---

## 4. Recommendations (prioritized)

**Before Early Submission (the graded W2 core):**
1. **FR-RAG-2** — dense pgvector retrieval + RRF fusion with the existing FTS + a rerank step. Biggest gap.
2. **FR-CITE-2** — the click-to-source bbox overlay (#96); citation data already flows.
3. **FR-OBS-W2-1/2** — emit the W2 metrics from the graph (ingestion, retrieval hits, per-worker latency,
   extraction confidence, eval outcome) **and** add the matching Grafana panels; add the **eval-regression
   alert** (NFR-SLO-W2). This unblocks R3 and the cost/latency report.
4. Add a handful of **RAG/evidence eval cases** (R2) once FR-RAG-2 exists.

**Before Final:**
5. **NFR-TRACE-W2** — wrap supervisor/workers in child spans off the request activity.
6. **NFR-API-W2** — commit the OpenAPI spec + extend the Bruno collection to the W2 endpoints.
7. **NFR-HEALTH-W2** — add a vector-index readiness check to `/ready`.
8. **NFR-PERF-W2** + **FR-OBS-W2-3** — Week 2 baseline profiles + the cost/latency report, from measured
   telemetry (depends on #3).

**Verify (quick confirmations, may already hold):**
9. FR-DOC-5 field confidence · FR-GRAPH-3 suppression on the evidence path · NFR-SEC-W2 store encryption ·
   NFR-CI-W2 dependency-audit/security-scan jobs.

---

*Audit reflects `develop` as of 2026-07-15. Status is from code inspection; "Not fully traced" items need a
short confirmation pass. Related: saga #71, and open issues #82 (FR-RAG), #96 (FR-CITE-2), #85/#57 (FR-OBS).*
