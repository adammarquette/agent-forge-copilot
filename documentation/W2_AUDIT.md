# W2_AUDIT.md — Week 2 implementation audit (Multimodal Evidence Agent)

**Audited:** the Week 2 implementation on `develop` against the requirements in [`W2_PRD.md`](W2_PRD.md)
(FR-/NFR-W2 IDs) and [`W2_ARCHITECTURE.md`](W2_ARCHITECTURE.md).
**Re-audited:** 2026-07-17 (supersedes the 2026-07-16 pass). **Method:** code inspection of `src/`,
`tests/`, `evals/`, `observability/`, `.gitlab/ci/`, and the deployed-config docs. **Scope:** implementation
coverage, gaps, and risk — *not* a security pen-test or a performance run.

> This audits *our sidecar's* Week 2 work. [`AUDIT.md`](AUDIT.md) is a separate audit of the OpenEMR fork.

**Legend:** ✅ Met · 🟡 Partial · ❌ Gap · Each finding cites the file(s) it's based on. "Not fully traced"
marks items asserted from a single reference rather than end-to-end verification.

> **What changed since the 2026-07-16 pass.** Three things moved — verified against the code, and for (2)
> exercised live on staging:
> **(1) The per-encounter story is now real** — the 2026-07-16 pass's *top* risk was "metered but not traced:
> you can't reconstruct one encounter." A turn now emits one correlation-scoped **`encounter.telemetry`** line
> carrying all seven FR-OBS-W2-1 signals, surfaced by a **"Per-encounter story"** Grafana/Loki panel, so a past
> encounter *is* answerable — via structured telemetry rather than spans. **(2) Click-to-source now actually
> works end-to-end live** — it was built but non-functional (an OpenEMR `patient/Binary.read` scope the client
> was never registered for made every fetch 401 → a misleading 404), and stale pre-#110 facts had null boxes;
> both are fixed, the scope grant is now reproducible (`tools/RegisterSmartClients`), and per-fact boxes are
> verified against the real PDFs. **(3) `ExtractionConfidence` is populated** — the 2026-07-16 "confirm the
> extractor populates it" caveat resolved to *no, it never did*; it now does.
>
> The remaining observability gap narrows to **span-level tracing** (NFR-TRACE-W2): spans are still not opened
> in the graph, and — newly confirmed — the tracer exports to **console only** (no OTLP/Tempo), so even the
> spans that exist are never collected.

---

## 1. Executive summary

The **trust spine, the hybrid RAG pipeline, the click-to-source overlay (now verified working live), the Week 2
metrics surface, and the per-encounter telemetry are all in place**, and the graded **50-case eval HARD GATE**
still blocks a regression (FR-EVAL-W2-*). Week 2's core deliverables are met. What's left is a tail of NFR
polish, most of it "Should"-tier:

1. **The graph is traced at the encounter level, but not as spans** — a turn now emits one correlation-scoped
   `encounter.telemetry` line (tool sequence, per-step + turn latency, tokens, cost, retrieval hits, extraction
   confidence, verification outcome), so **one encounter can be reconstructed** from the Grafana "Per-encounter
   story" panel (FR-OBS-W2-1 ✅). What's still absent is a **span waterfall**: no layer of the evidence graph
   opens an `ActivitySource` span, the evidence endpoint bypasses the Week-1 orchestrator that does, and the
   tracer is wired to `AddConsoleExporter()` only — no OTLP/Tempo backend — so spans are never collected
   (NFR-TRACE-W2). **Still the top remaining gap, but no longer blocks answering "what happened in this
   encounter".**
2. **The eval gate still doesn't cover RAG** — the golden set is 50 cases, all extraction (intake-*/lab-*);
   there are still no evidence-retrieval / grounding cases, so a RAG regression wouldn't turn CI red even
   though RAG now ships (FR-EVAL coverage caveat).
3. **The declared-but-thin items** — no committed OpenAPI spec / no W2 API-collection coverage, `/ready`
   doesn't check the vector index, `DerivedFactStore` at-rest encryption is intended-not-applied, no Week 2
   performance baselines, and the cost/latency *report* (a Final deliverable) isn't produced yet (its telemetry
   now exists).

### Status at a glance

| Group | Status | One-line |
|---|---|---|
| FR-DOC — ingestion & extraction | ✅ Met | Schema-gated extraction, idempotent ingest, no write-back; `ExtractionConfidence` now **populated**, not just modeled |
| FR-RAG — hybrid retrieval + rerank | ✅ Met | Dense pgvector(1536)+HNSW **+** sparse FTS **+** RRF fusion **+** Cohere rerank, degrades gracefully |
| FR-GRAPH — supervisor + workers | ✅ Met | Typed graph, now 4 workers; handoffs logged **and** metered |
| FR-CITE — citation contract + overlay | ✅ Met | Contract ✅; overlay **verified working live** — per-fact boxes highlight the cited row (#96/#109/#128/#130/#136) |
| FR-EVAL — golden set + PR gate | ✅ Met | 50 cases, 5 boolean rubrics, gate blocks a regression (**RAG cases still absent — see R2**) |
| FR-OBS — Week 2 telemetry + panels | 🟡 Partial | Metrics + Week-2 Grafana row ✅; **per-encounter telemetry + "Per-encounter story" panel ✅ (W2-1 met)**; **cost/latency report (W2-3) pending** |
| NFR-CONTRACT-W2 | ✅ Met | Typed handoff/extraction contracts + contract tests |
| NFR-TRACE-W2 | ❌ Gap | No ActivitySource spans in the graph; evidence path gets no root span; tracer exports to **console only** (no OTLP/Tempo) — spans never collected. *Per-encounter reconstruction is met via telemetry (FR-OBS-W2-1), just not as a span waterfall* |
| NFR-LOG-W2 | ✅ Met | W2 events extend the Week 1 structured-log schema |
| NFR-SLO-W2 | 🟡 Partial | 5 alerts incl. **retrieval-degradation**; eval-regression is a CI hard gate, not a runtime alert; no explicit ingestion/retrieval SLO alert |
| NFR-HEALTH-W2 | 🟡 Partial | `/ready` checks OpenEMR/LLM/Prometheus; **not the vector index** |
| NFR-CI-W2 | ✅ Met | evals gate + deterministic rubric tests in the pipeline |
| NFR-API-W2 | 🟡 Partial | Runtime OpenAPI only; **no committed spec, no W2 collection coverage** |
| NFR-TEST-W2 | ✅ Met | Hermetic fixture/stub tests; strategy documented |
| NFR-DATA/MIGRATE-W2 | ✅ Met | One-authority model; vector column + HNSW index in the migration |
| NFR-SEC-W2 — PHI | 🟡 Partial | `no_phi_in_logs` rubric ✅; **DerivedFact at-rest encryption still intended-not-applied** |
| NFR-BACKUP-W2 | 🟡 Partial | Documented; eval set repo-reproducible; recovery untested |
| NFR-PERF-W2 | ❌ Gap | No Week 2 baselines in PERFORMANCE_BASELINES.md (Should); now unblocked by telemetry |

---

## 2. Detailed findings

### 2.1 FR-DOC — Document ingestion & extraction — ✅ Met
- **FR-DOC-1/2 (ingestion tool + schema-as-gate):** `DocumentIngestionService`, `IngestionEndpoints`
  (`POST /documents/ingest`), and `DocumentExtractor` validate against strict schemas (`LabExtraction`,
  `IntakeExtraction`) and reject malformed output with a `RejectionReason`. Raw VLM output does not bypass
  validation. ✅
- **FR-DOC-3 (write-back + authority + idempotency):** `ContentHash` + `IngestedDocument` give content-hash
  idempotency; OpenEMR stays authoritative, derived facts are sidecar-owned (`DerivedFactStore`), no
  Observation write-back — matches the E2 pivot. ✅
- **FR-DOC-4/5 (required fields + confidence):** extraction schemas carry the required fields and per-fact
  citations; `DerivedFact.ExtractionConfidence` is a real `double?`
  ([DerivedFact.cs:29](../src/MarqSpec.AgentForge.Data/Entities/DerivedFact.cs)). The 2026-07-16 "*confirm the
  extractor actually populates it*" caveat has been **checked and closed**: it did **not** — the column was
  always null in practice — and it now **is** populated. `DerivedFactMapper` sets it at all four fact sites via
  `ConfidenceFrom(citation)`: **1.0** when the extractor located the exact quote in the source PDF (a resolved
  bounding box), **0.5** when only page-level
  ([DerivedFactMapper.cs:109](../src/MarqSpec.AgentForge.Agents/Ingestion/DerivedFactMapper.cs)). Note this is
  a *grounding/locatability* confidence, not a model-reported one — the extraction schema does not ask the model
  for a confidence value. ✅

### 2.2 FR-RAG — Hybrid retrieval + rerank — ✅ Met (headline gap now closed)
- **FR-RAG-1 (corpus):** `GuidelineCorpusSeeder` builds a committed guideline corpus. ✅
- **FR-RAG-2 (hybrid + rerank):** **all four stages exist.** `HybridEvidenceRetriever` fuses a **dense** half
  (`DenseEvidenceRetriever` over a pgvector `vector(1536)` column with an **HNSW** `vector_cosine_ops` index)
  and a **sparse** half (`FtsEvidenceRetriever`, Postgres FTS) via **`ReciprocalRankFusion`**, then applies a
  **cross-encoder rerank** (`CohereReranker`, rerank-v3.5) with embeddings from `CohereEmbeddingProvider`
  (embed-v4). On any stage failure it degrades deterministically and continues — recorded via
  `RecordRetrievalDegradation(stage)` ([HybridEvidenceRetriever.cs](../src/MarqSpec.AgentForge.Retrieval/HybridEvidenceRetriever.cs)).
  ✅
- **FR-RAG-3 (evidence vs record facts):** `CitationSourceType` separates `guideline` from `fhir`/`derived`. ✅

### 2.3 FR-GRAPH — Supervisor + workers — ✅ Met
- `EvidenceAgentSupervisor` routes over typed state through **four** workers —
  `intake-extractor → evidence-retriever → answer-composer → critic` — logging **and metering** every handoff
  (`RecordRoutingDecision`) ([EvidenceAgentSupervisor.cs](../src/MarqSpec.AgentForge.Agents/EvidenceAgentSupervisor.cs)).
  Contract-tested (`EvidenceAgentSupervisorTests`). ✅
- **FR-GRAPH-3 (critic gate):** the critic **is** the Week 1 `IClinicalResponseVerifier`, reused as a node; it
  suppresses uncited claims and surfaces domain-constraint flags. `BuildToolResults` projects lab, derived, and
  **guideline** citations into the scanner's `[ResourceType/Id]` shape, so guideline/evidence citations resolve
  instead of being suppressed — closing the 2026-07-15 "does suppression cover guideline citations?" caveat. ✅
- ⚠️ The graph is now **logged and metered, but still not traced** — see NFR-TRACE-W2.

### 2.4 FR-CITE — Citation contract + click-to-source — ✅ Met (headline gap now closed)
- **FR-CITE-1 (machine-readable citation):** `Citation` carries `SourceType/SourceId/PageOrSection/
  FieldOrChunkId/QuoteOrValue` and a normalized `BoundingBox`. ✅
- **FR-CITE-2 (bbox overlay UI):** built **and now verified working end-to-end on staging**. The box is computed
  from the PDF's own glyph geometry (PdfPig), not a VLM estimate. Two paths ship: the demo upload path (#96) and
  the **production read path** (#109) that reads a persisted `DerivedFact`, fetches the OpenEMR `Binary` over
  FHIR, and overlays the exact region — now also in the main chat SPA (`wwwroot/index.html`), where each cited
  fact opens **its own** region (#136). ✅
  <br>*Worth recording, because "built" hid three defects until exercised live:* (a) every `Binary` fetch 401'd
  because OpenEMR's `finalizeScopes` silently drops a requested scope the client isn't **registered** for, and
  the SMART client had no `patient/Binary.read` — surfacing as a misleading 404 (#128; the grant is now
  reproducible via `tools/RegisterSmartClients`); (b) all demo facts had **null** boxes because they were
  ingested ~18h *before* the box-resolution code (#110) shipped — the resolver was correct, the data was stale
  (#130); (c) the overlay opened `facts[0]`'s box for every citation (#136). The lesson for the remaining
  "declared-but-thin" tail below: **a code-inspection ✅ is not an exercised ✅.**

### 2.5 FR-EVAL — Golden set + PR-blocking gate — ✅ Met (the graded HARD GATE)
- **FR-EVAL-W2-1:** exactly **50** golden cases (`evals/golden/*.json`). ✅
- **FR-EVAL-W2-2:** all five boolean rubrics present (`RubricEvaluator`): `schema_valid`, `citation_present`,
  `factually_consistent`, `safe_refusal`, `no_phi_in_logs`; deterministic ones also run as xUnit theories
  (the `eval-tests` job in `.github/workflows/ci.yml`). ✅
- **FR-EVAL-W2-3:** the `evals` job (`.github/workflows/ci.yml`) fails the pipeline on a threshold/regression breach
  vs `evals/baseline.json` — verified by injecting a regression. ✅
- **FR-EVAL-W2-4:** refusal / missing-data / not-JSON cases are in the set. ✅
- ⚠️ **Coverage caveat (still open — see R2):** the golden set is entirely `intake-*` / `lab-*` extraction
  cases, and `RubricEvaluator` scores a `DocumentExtractionResult`. There are **still no evidence-retrieval /
  RAG-grounding cases**, so the gate does not guard FR-RAG behavior — now a sharper gap, because FR-RAG-2 has
  shipped since the last pass.

### 2.6 FR-OBS — Week 2 observability & cost — 🟡 Partial (W2-1 ✅ + panels ✅; report pending)
- **FR-OBS-W2-1 (per-encounter W2 telemetry): ✅ Met.** Two layers now exist, and the *per-encounter* one is
  what the requirement actually asks for ("each encounter **logs** … all seven answerable for any past
  encounter"):
  - **Aggregate metrics** fire at real call sites — `IAgentForgeMetrics` gained document-ingestion
    (outcome+latency), per-worker latency, routing decisions, evidence-retrieval hit/count/latency, rerank
    latency, and retrieval degradation, from `EvidenceAgentSupervisor`, `HybridEvidenceRetriever`, and
    `DocumentIngestionService`.
  - **Per-encounter telemetry** (#135): `AgentOrchestrator` emits one correlation-scoped **`encounter.telemetry`**
    line at the verified final answer carrying **all seven** signals — tool sequence, per-tool + turn latency,
    input/output tokens, cost, retrieval hits (from `retrieve_evidence` results), extraction confidence (from
    `get_document_facts`), and the **eval outcome** = the runtime verification result (passed + suppressed
    claims). No PHI: tool names and numbers only; the correlation id (not a patient id) is the encounter key
    ([AgentOrchestratorLog.cs](../src/MarqSpec.AgentForge.Agent/AgentOrchestratorLog.cs)).
    <br>*Interpretation note:* the golden-set **eval is a CI-only offline gate**, so the per-encounter runtime
    analog of "eval outcome" is the verification/grounding gate — confirmed with the product owner.
    <br>*Coverage caveats (added by the 2026-07-17 re-verification pass):* (a) the line is emitted **only on the
    verified-final-answer path** ([AgentOrchestrator.cs:232-244](../src/MarqSpec.AgentForge.Agent/AgentOrchestrator.cs)) —
    every deterministic-fallback exit (LLM failure, turn-deadline, tool-round-budget exceeded, still-malformed
    output) returns via `BuildDeterministicFallback` **without** emitting it, so a **degraded/failed encounter
    leaves no per-encounter record**. Against the FR-OBS-W2-1 "answerable for *any* past encounter" wording this
    is a real edge: the encounters most worth inspecting (the failures) are exactly the ones not logged.
    (b) the standalone Week-2 `/evidence/ask` supervisor graph does **not** emit this line — it bypasses
    `AgentOrchestrator` (see NFR-TRACE-W2). Evidence retrieval **is** captured when the Week-1 agent drives
    `retrieve_evidence` / `get_document_facts` as MCP tools (those turns get a line with `retrieval_hits` /
    `extraction_confidence` populated), but the dedicated evidence endpoint is not — which bounds what the
    cost/latency report (FR-OBS-W2-3) can measure from `encounter.telemetry` to the chat/brief/agenda flows.
- **FR-OBS-W2-2 (W2 dashboard panels):** the Grafana dashboard carries the **"Week 2 — Multimodal Evidence
  Agent"** row (ingestion rate/latency, per-worker latency p95, routing decisions, evidence-retrieval hit rate
  + latency, rerank latency p50/p95, retrieval-degradations-by-stage) **plus a "Per-encounter story" Loki panel**
  over `encounter.telemetry` — filter one `CorrelationId` to answer all seven for a single past encounter
  ([agentforge.json](../observability/grafana/dashboards/agentforge.json)). ✅
- **FR-OBS-W2-3 (cost & latency report):** **not produced** (a Final deliverable). The telemetry to generate it
  from measured data now exists; the report itself does not. ❌ pending.

### 2.7 NFR findings
- **NFR-CONTRACT-W2 — ✅** typed handoff + canonical extraction schemas, contract-tested.
- **NFR-TRACE-W2 — ❌ (top remaining gap, but narrower now)** no distributed tracing across the graph.
  `AgentForgeActivitySource` is used only in `.Agent`/`.Mcp` (`agent.turn`, `llm.complete`); the evidence graph
  opens **no** spans, and `EvidenceEndpoints.HandleAskAsync` calls `IEvidenceAgentSupervisor` directly,
  **bypassing** the orchestrator that would have started a root span — so an evidence ask has no trace at all.
  **Newly confirmed (closing the 2026-07-16 "confirm the OTel tracer exports" question): it does not.**
  `WithTracing(...)` is wired to **`AddConsoleExporter()` only** — there is no OTLP trace exporter and no traces
  backend (the stack is Prometheus + Loki; no Tempo)
  ([Program.cs](../src/MarqSpec.AgentForge.Api/Program.cs)). So even the spans that *are* opened go to stdout
  and are never collected or queryable.
  <br>**What this no longer blocks:** "reconstruct one encounter" is now answered by the per-encounter
  `encounter.telemetry` line + its Grafana panel (FR-OBS-W2-1 ✅). The residual gap is the **span waterfall** —
  per-step causality/nesting and cross-service correlation — which telemetry lines don't give you. Closing it
  means both opening spans in the graph **and** adding a trace backend + OTLP exporter.
- **NFR-LOG-W2 — ✅** W2 log events extend the Week 1 schema (`*Log.cs` partial-class pattern).
- **NFR-SLO-W2 — 🟡** `observability/alerts/agentforge-alerts.yml` now has **5** rules including
  `AgentForgeRetrievalDegradation` (fires on any `dense`/`sparse`/`rerank` fallback). The eval-regression
  control is the **PR-blocking CI hard gate** (FR-EVAL-W2-3), not a Prometheus alert — arguably the stronger
  control, but note it's not a runtime alert. No explicit ingestion/retrieval-latency SLO alert yet.
- **NFR-HEALTH-W2 — 🟡** `/ready` aggregates `OpenEmrHealthCheck`, `LlmProviderHealthCheck`,
  `ObservabilityHealthCheck` (degraded-aware) — but **still no vector-index (Postgres/pgvector) readiness
  check** ([Program.cs:241](../src/MarqSpec.AgentForge.Api/Program.cs)). The reranker has no check but degrades
  gracefully, so its absence is non-fatal.
- **NFR-CI-W2 — ✅** evals gate + deterministic rubric theories in the pipeline. *Not fully traced:*
  dependency-audit + security-scan on every PR (confirm present in CI).
- **NFR-API-W2 — 🟡** runtime OpenAPI via `Microsoft.AspNetCore.OpenApi` exists, but **no committed OpenAPI
  spec**, no contract test asserting impl-vs-spec sync, and the Bruno collection still covers only
  Chat/Health/Launch/Metrics — **not** the W2 endpoints (`/documents/ingest`, evidence ask/document). Unchanged
  since the last pass.
- **NFR-TEST-W2 — ✅** hermetic fixture/stub eval tests run offline in CI; unit + BFF integration tests present.
- **NFR-DATA / MIGRATE-W2 — ✅** one-authority model; the `InitialCreate` migration provisions the pgvector
  extension, the `vector(1536)` embedding column, and the HNSW index alongside the tsvector FTS index.
- **NFR-SEC-W2 — 🟡** `no_phi_in_logs` rubric *is* the CI PHI check (met). The `DerivedFact` "PHI at rest"
  surface is **documented but not encrypted** — the entity comment says "treat accordingly (§12)," but no
  column/at-rest encryption is applied. "Intended ≠ applied" still stands (R4).
- **NFR-BACKUP-W2 — 🟡** documented; the eval set is repo-reproducible (✅ that part); recovery procedure
  untested.
- **NFR-PERF-W2 — ❌** no Week 2 baseline profiles in `PERFORMANCE_BASELINES.md` (a "Should"); now unblocked by
  the metrics above.

---

## 3. Key risks

| # | Risk | Why it matters |
|---|---|---|
| R1 | **No span waterfall; tracer exports to console only** (NFR-TRACE-W2) | *Downgraded:* reconstructing one encounter is now met via `encounter.telemetry` + its panel (FR-OBS-W2-1 ✅). What's left: the graph opens no spans, the evidence path has no root span, and the tracer has no OTLP/Tempo backend — so per-step causality and cross-service correlation are unavailable. Top remaining observability gap, no longer a blocker for the per-encounter story. |
| R2 | **Eval gate doesn't cover RAG** (FR-EVAL coverage) | The HARD GATE guards extraction but not evidence grounding — and RAG now ships, so a retrieval/rerank regression wouldn't turn CI red. |
| R3 | **DerivedFact at-rest encryption unverified** (NFR-SEC-W2) | New PHI-at-rest surface (RW3); "documented" ≠ "encrypted". |
| R4 | **No committed API spec / W2 collection** (NFR-API-W2) | A grader can't run the W2 workflows (`/documents/ingest`, evidence ask) from a collection without reading source. |
| R5 | **No W2 perf baselines / cost report** (NFR-PERF-W2, FR-OBS-W2-3) | Telemetry exists but the measured baselines and the cost/latency report — a Final deliverable — aren't produced. |

*(R1/R3 from the 2026-07-15 pass — "hybrid RAG advertised, sparse-only shipped" and "W2 graph is a black box to
telemetry" — are resolved and dropped.)*

---

## 4. Recommendations (prioritized)

**Remaining before Final (the graded W2 core is met):**
1. **NFR-TRACE-W2** — two halves, both required: (a) wrap the evidence endpoint in a root `Activity` and open a
   child span per supervisor worker off the existing `AgentForgeActivitySource` (the plumbing exists; the workers
   just don't call it); (b) **add a trace backend + OTLP exporter** — the tracer is `AddConsoleExporter()`-only
   today, so spans go nowhere (a Tempo service alongside Prometheus/Loki, exported like the Loki logs already
   are). Closes R1. *Lower urgency than the 2026-07-16 pass implied:* the cost/latency report (rec. 6) can now be
   backed by `encounter.telemetry` without spans.
2. **FR-EVAL RAG coverage** — add a handful of evidence-retrieval / grounding golden cases so the gate guards
   FR-RAG behavior (R2).
3. **NFR-API-W2** — commit the OpenAPI spec + extend the Bruno collection to the W2 endpoints (R4).
4. **NFR-SEC-W2** — apply (or explicitly document the deferral of) at-rest encryption on the `DerivedFact`
   store (R3).
5. **NFR-HEALTH-W2** — add a vector-index/Postgres readiness check to `/ready`.
6. **NFR-PERF-W2 + FR-OBS-W2-3** — capture Week 2 baseline profiles and produce the cost/latency report from
   the now-emitted telemetry (R5).

**Verify (quick confirmations, may already hold):**
7. NFR-CI-W2 dependency-audit/security-scan jobs. *(The 2026-07-16 "confirm `ExtractionConfidence` is populated"
   item is now closed — checked, was null, fixed; see §2.1.)*

---

## 5. Base-system findings carried forward (from AUDIT.md)

> **Why this section exists.** [`AUDIT.md`](AUDIT.md) is the pre-agent baseline of the OpenEMR fork, preserved
> as a historical snapshot and never edited. Its findings must not be lost as the weekly audits move on, so
> **each weekly implementation audit carries the base findings forward with their current status** — the role
> the retired `AUDIT_DELTA.md` used to play, now folded in here so there is one living audit rather than a
> separate delta doc to keep in sync. Next week's audit inherits and updates this table.
> **Legend:** ✅ Addressed · 🟡 Partial · ➖ Still open · 🔀 Superseded.

| Base finding (AUDIT.md §) | Status | Where it stands now |
|---|---|---|
| §1/§2.1 Dev-compose weak creds + exposed side-channels | 🔀 Superseded | Railway staging behind a reverse proxy replaced the dev compose; sidecar on the private network, public domain removed. No longer the deployment path. |
| §2.2 No per-patient authorization | ✅ Addressed | Sidecar is patient-scoped by construction — a session binds one `PatientSessionContext`, cross-patient asks refused (FR-CHAT-3). The agent inherits no blanket "read all patients" capability. |
| §2.3 Injection | ➖ Unchanged | Base posture holds; the sidecar adds no SQL path into OpenEMR (FHIR/REST only). |
| §2.4 `cookie_samesite` Strict→Lax for SMART launch | 🟡 Partial | Fork default is back to `Strict`, with `Lax`/`None` used per-context for the OAuth2/SMART session. *Open:* confirm agent-forge#26 formally closed (tracked as sidecar #63). |
| §2.5 Crypto keys on the same volume as data | ➖ **Still open** | Production KMS/secret-store still needed; sidecar secrets are env/Options, no secrets in source. |
| §3.1–3.3 Perf (no cache/replica/FT index; retrieval off the primary DB) | ✅ Followed | Retrieval + embeddings live in the sidecar's own Postgres (pgvector + HNSW), not OpenEMR's MySQL — exactly the §3.2 recommendation. Latency is LLM-dominated as predicted. |
| §4.4 Separate OAuth2 FHIR client + custom module | ✅ Followed | Sidecar consumes SMART/OAuth FHIR under a scoped token; the UI ships as the `oe-module-agentforge` custom module. |
| §5.1 Fragmented meds (two-table model) | 🟡 Partial | Agent reads FHIR `MedicationRequest`/`MedicationDispense`; the legacy `lists[type=medication]` source is not separately read. The schema fragmentation itself is unchanged. |
| §5.2 Empty demo DB | ✅ Addressed | 7 synthetic cardiology patients seeded with problems/allergies/meds/encounters/labs (`seed_cardiology_demo.php`). *Not re-measured:* a fresh live staging row-count. |
| §5.3 Failure modes (single-source meds, free-text-as-coded, null DOB, orphans) | 🟡 Mitigated | Source-attribution gate + critic/verifier target these; they remain verification problems by design, not eliminated. |
| §6.1 Audit config-gated; base won't auto-log a service account's reads | ✅ Addressed | `AuditingMcpToolServer` + `AccessAuditLog` record clinician + patient + tool + correlation id on every tool call; the agent reads under the clinician's own SMART token, so OpenEMR's native audit attributes the reads too. |
| §6.2 No automated retention/purge | ➖ **Still open** | HIPAA retention schedule undefined; out of the agent build's scope. |
| §6.3 No breach detection/notification | ➖ **Still open** | The sidecar has an alerting surface, but no breach-notification workflow. |
| §6.4 BAA / PHI-to-LLM disclosure discipline | ✅ Addressed (architecture) | Correlation id per call; no PHI in general logs (CI `no_phi_in_logs`); minimum-necessary field selection; access-audit stream kept distinct; TLS in transit; demo-data-only holds. |

**Still open from the base system (unchanged, carried forward):** crypto keys on the same volume (§2.5),
automated audit retention/purge (§6.2), breach detection/notification (§6.3), schema-level data fragmentation
(§5.1), and base safety toggles remaining opt-out (§2.1/§6.1). These are base-EHR policy/infra gaps the agent
build never claimed to fix; the agent's own always-on audit choke point backstops, but does not replace, the
base toggles.

---

*Re-audit reflects `develop` as of 2026-07-17 (supersedes 2026-07-16, which superseded 2026-07-15). Status is
from code inspection, plus **live verification on staging** for FR-CITE-2 (which is how three defects behind a
code-inspection ✅ were found — see §2.4). **Independently re-verified 2026-07-17 (post-early-submission):**
FR-RAG (all four stages — dense pgvector(1536)+HNSW, sparse FTS, RRF, Cohere rerank — live and DI-wired from
both `/evidence/ask` and the chat `retrieve_evidence` tool, not orphaned), FR-OBS-W2-1 (seven-signal line
confirmed; two coverage caveats added to §2.6), NFR-TRACE (no graph spans; tracer console-only — the one OTLP
exporter belongs to the logs→Loki pipeline, not traces), and the NFR-API/HEALTH/SEC "declared-but-thin" gaps
all confirmed against code — **no finding changed**. "Not fully traced" items need a short confirmation pass.
Related:
saga #71. Closed since the 2026-07-16 pass — #135 (FR-OBS-W2-1 per-encounter telemetry), #128 (Binary scope +
reproducible client registration), #130/#136 (click-to-source boxes), #131/#132 (citation UX + prompt), #126
(brief latency), #127 (CI deploy false-fail). Earlier: #82 (FR-RAG), #96/#109 (FR-CITE-2), #85/#57 + W2 metrics
(FR-OBS). Open tail: NFR-TRACE (span waterfall + trace backend), NFR-API, NFR-SEC, NFR-PERF / FR-OBS-W2-3.*
