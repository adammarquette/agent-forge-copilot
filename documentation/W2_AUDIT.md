# W2_AUDIT.md — Week 2 implementation audit (Multimodal Evidence Agent)

**Audited:** the Week 2 implementation on `develop` against the requirements in [`W2_PRD.md`](W2_PRD.md)
(FR-/NFR-W2 IDs) and [`W2_ARCHITECTURE.md`](W2_ARCHITECTURE.md).
**Re-audited:** 2026-07-16 (supersedes the 2026-07-15 first pass). **Method:** code inspection of `src/`,
`tests/`, `evals/`, `observability/`, `.gitlab/ci/`, and the deployed-config docs. **Scope:** implementation
coverage, gaps, and risk — *not* a security pen-test or a performance run.

> This audits *our sidecar's* Week 2 work. [`AUDIT.md`](AUDIT.md) is a separate audit of the OpenEMR fork.

**Legend:** ✅ Met · 🟡 Partial · ❌ Gap · Each finding cites the file(s) it's based on. "Not fully traced"
marks items asserted from a single reference rather than end-to-end verification.

> **What changed since the 2026-07-15 pass.** All three headline gaps have since landed and are verified in
> code: **hybrid retrieval + rerank** (FR-RAG-2), the **click-to-source overlay** (FR-CITE-2), and the
> **Week 2 observability surface** — metrics *and* dashboard panels (FR-OBS-W2-1/2). The graph also grew from
> two workers to four (added `answer-composer` + `critic`). The remaining gaps are the smaller
> "declared-but-thin" tail, now led by **distributed tracing** (NFR-TRACE-W2), which is the one observability
> half still missing.

---

## 1. Executive summary

The **trust spine, the hybrid RAG pipeline, the click-to-source overlay, and the Week 2 metrics surface are all
in place**, and the graded **50-case eval HARD GATE** still blocks a regression (FR-EVAL-W2-*). Week 2's core
deliverables are met. What's left is a tail of NFR polish, most of it "Should"-tier:

1. **The graph is metered but not *traced*** — every worker records latency/hit/routing metrics, but no layer
   of the evidence graph opens an `ActivitySource` span, and the evidence endpoint bypasses the Week-1
   orchestrator that does. So you can aggregate per-worker latency, but you can't reconstruct one encounter as
   a span waterfall (FR-OBS-W2 tracing / NFR-TRACE-W2). **This is the top remaining gap.**
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
| FR-DOC — ingestion & extraction | ✅ Met | Schema-gated extraction, idempotent ingest, no write-back; `ExtractionConfidence` modeled |
| FR-RAG — hybrid retrieval + rerank | ✅ Met | Dense pgvector(1536)+HNSW **+** sparse FTS **+** RRF fusion **+** Cohere rerank, degrades gracefully |
| FR-GRAPH — supervisor + workers | ✅ Met | Typed graph, now 4 workers; handoffs logged **and** metered |
| FR-CITE — citation contract + overlay | ✅ Met | Contract ✅; **click-to-source overlay built** (`evidence.html`, #96/#109) |
| FR-EVAL — golden set + PR gate | ✅ Met | 50 cases, 5 boolean rubrics, gate blocks a regression (**RAG cases still absent — see R2**) |
| FR-OBS — Week 2 telemetry + panels | 🟡 Partial | Metrics emitted + full Week-2 Grafana row ✅; **cost/latency report (W2-3) pending** |
| NFR-CONTRACT-W2 | ✅ Met | Typed handoff/extraction contracts + contract tests |
| NFR-TRACE-W2 | ❌ Gap | Graph has **no** ActivitySource spans; evidence path gets no root span at all |
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
  citations; `DerivedFact.ExtractionConfidence` is a real `double?` (value in `[0,1]` when reported, **null**
  when not — a null, not a guess) ([DerivedFact.cs:29](../src/GauntletAI.AgentForge.Data/Entities/DerivedFact.cs)).
  ✅ *Not fully traced:* confirm the extractor actually populates it end-to-end for both schemas.

### 2.2 FR-RAG — Hybrid retrieval + rerank — ✅ Met (headline gap now closed)
- **FR-RAG-1 (corpus):** `GuidelineCorpusSeeder` builds a committed guideline corpus. ✅
- **FR-RAG-2 (hybrid + rerank):** **all four stages exist.** `HybridEvidenceRetriever` fuses a **dense** half
  (`DenseEvidenceRetriever` over a pgvector `vector(1536)` column with an **HNSW** `vector_cosine_ops` index)
  and a **sparse** half (`FtsEvidenceRetriever`, Postgres FTS) via **`ReciprocalRankFusion`**, then applies a
  **cross-encoder rerank** (`CohereReranker`, rerank-v3.5) with embeddings from `CohereEmbeddingProvider`
  (embed-v4). On any stage failure it degrades deterministically and continues — recorded via
  `RecordRetrievalDegradation(stage)` ([HybridEvidenceRetriever.cs](../src/GauntletAI.AgentForge.Retrieval/HybridEvidenceRetriever.cs)).
  ✅
- **FR-RAG-3 (evidence vs record facts):** `CitationSourceType` separates `guideline` from `fhir`/`derived`. ✅

### 2.3 FR-GRAPH — Supervisor + workers — ✅ Met
- `EvidenceAgentSupervisor` routes over typed state through **four** workers —
  `intake-extractor → evidence-retriever → answer-composer → critic` — logging **and metering** every handoff
  (`RecordRoutingDecision`) ([EvidenceAgentSupervisor.cs](../src/GauntletAI.AgentForge.Agents/EvidenceAgentSupervisor.cs)).
  Contract-tested (`EvidenceAgentSupervisorTests`). ✅
- **FR-GRAPH-3 (critic gate):** the critic **is** the Week 1 `IClinicalResponseVerifier`, reused as a node; it
  suppresses uncited claims and surfaces domain-constraint flags. `BuildToolResults` projects lab, derived, and
  **guideline** citations into the scanner's `[ResourceType/Id]` shape, so guideline/evidence citations resolve
  instead of being suppressed — closing the 2026-07-15 "does suppression cover guideline citations?" caveat. ✅
- ⚠️ The graph is now **logged and metered, but still not traced** — see NFR-TRACE-W2.

### 2.4 FR-CITE — Citation contract + click-to-source — ✅ Met (headline gap now closed)
- **FR-CITE-1 (machine-readable citation):** `Citation` carries `SourceType/SourceId/PageOrSection/
  FieldOrChunkId/QuoteOrValue` and a normalized `BoundingBox`. ✅
- **FR-CITE-2 (bbox overlay UI):** built. `wwwroot/evidence.html` renders the source document and highlights
  the cited region on demand; the box is computed from the PDF's own glyph geometry (PdfPig), not a VLM
  estimate. Two paths ship: the demo upload path (#96) and the **production read path** (#109) that reads a
  persisted `DerivedFact`, fetches the OpenEMR `Binary` over FHIR, and overlays the exact region. ✅

### 2.5 FR-EVAL — Golden set + PR-blocking gate — ✅ Met (the graded HARD GATE)
- **FR-EVAL-W2-1:** exactly **50** golden cases (`evals/golden/*.json`). ✅
- **FR-EVAL-W2-2:** all five boolean rubrics present (`RubricEvaluator`): `schema_valid`, `citation_present`,
  `factually_consistent`, `safe_refusal`, `no_phi_in_logs`; deterministic ones also run as xUnit theories
  (`.gitlab/ci/test.yml`). ✅
- **FR-EVAL-W2-3:** the `evals` job (`.gitlab/ci/evals.yml`) fails the pipeline on a threshold/regression breach
  vs `evals/baseline.json` — verified by injecting a regression. ✅
- **FR-EVAL-W2-4:** refusal / missing-data / not-JSON cases are in the set. ✅
- ⚠️ **Coverage caveat (still open — see R2):** the golden set is entirely `intake-*` / `lab-*` extraction
  cases, and `RubricEvaluator` scores a `DocumentExtractionResult`. There are **still no evidence-retrieval /
  RAG-grounding cases**, so the gate does not guard FR-RAG behavior — now a sharper gap, because FR-RAG-2 has
  shipped since the last pass.

### 2.6 FR-OBS — Week 2 observability & cost — 🟡 Partial (metrics + panels ✅; report + tracing pending)
- **FR-OBS-W2-1 (per-encounter W2 telemetry):** **now emitted at real call sites**, not just declared.
  `IAgentForgeMetrics` gained document-ingestion (outcome+latency), per-worker latency, routing decisions,
  evidence-retrieval hit/count/latency, rerank latency, and retrieval degradation; they fire from
  `EvidenceAgentSupervisor`, `HybridEvidenceRetriever`, and `DocumentIngestionService`. The old "`.Agents`
  references neither `IAgentForgeMetrics` nor any ActivitySource" finding is half-resolved: **metrics yes,
  spans no** (tracing tracked under NFR-TRACE-W2). ✅ (metrics)
- **FR-OBS-W2-2 (W2 dashboard panels):** the Grafana dashboard now carries a **"Week 2 — Multimodal Evidence
  Agent"** row: ingestion rate/latency, per-worker latency p95, routing decisions, evidence-retrieval hit rate
  + latency, rerank latency p50/p95, and retrieval-degradations-by-stage
  ([agentforge.json](../observability/grafana/dashboards/agentforge.json)). ✅
- **FR-OBS-W2-3 (cost & latency report):** **not produced** (a Final deliverable). The telemetry to generate it
  from measured data now exists; the report itself does not. ❌ pending.

### 2.7 NFR findings
- **NFR-CONTRACT-W2 — ✅** typed handoff + canonical extraction schemas, contract-tested.
- **NFR-TRACE-W2 — ❌ (top remaining gap)** no distributed tracing across the graph. `AgentForgeActivitySource`
  is used only in `.Agent`/`.Mcp` (`agent.turn`, `llm.complete`); the evidence graph opens **no** spans, and
  `EvidenceEndpoints.HandleAskAsync` calls `IEvidenceAgentSupervisor` directly, **bypassing** the orchestrator
  that would have started a root span — so an evidence ask has no trace at all. Metrics are aggregate
  (dimensioned by worker/stage/outcome), not per-encounter; correlation-ID logging exists, but "reconstruct a
  full multi-agent trace from the correlation ID" is not met at the span level.
- **NFR-LOG-W2 — ✅** W2 log events extend the Week 1 schema (`*Log.cs` partial-class pattern).
- **NFR-SLO-W2 — 🟡** `observability/alerts/agentforge-alerts.yml` now has **5** rules including
  `AgentForgeRetrievalDegradation` (fires on any `dense`/`sparse`/`rerank` fallback). The eval-regression
  control is the **PR-blocking CI hard gate** (FR-EVAL-W2-3), not a Prometheus alert — arguably the stronger
  control, but note it's not a runtime alert. No explicit ingestion/retrieval-latency SLO alert yet.
- **NFR-HEALTH-W2 — 🟡** `/ready` aggregates `OpenEmrHealthCheck`, `LlmProviderHealthCheck`,
  `ObservabilityHealthCheck` (degraded-aware) — but **still no vector-index (Postgres/pgvector) readiness
  check** ([Program.cs:241](../src/GauntletAI.AgentForge.Api/Program.cs)). The reranker has no check but degrades
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
| R1 | **W2 graph is metered but not *traced*** (NFR-TRACE-W2) | Per-worker metrics are aggregate; without spans you can't reconstruct one encounter end-to-end, and the evidence path has no root span at all. Top remaining observability gap. |
| R2 | **Eval gate doesn't cover RAG** (FR-EVAL coverage) | The HARD GATE guards extraction but not evidence grounding — and RAG now ships, so a retrieval/rerank regression wouldn't turn CI red. |
| R3 | **DerivedFact at-rest encryption unverified** (NFR-SEC-W2) | New PHI-at-rest surface (RW3); "documented" ≠ "encrypted". |
| R4 | **No committed API spec / W2 collection** (NFR-API-W2) | A grader can't run the W2 workflows (`/documents/ingest`, evidence ask) from a collection without reading source. |
| R5 | **No W2 perf baselines / cost report** (NFR-PERF-W2, FR-OBS-W2-3) | Telemetry exists but the measured baselines and the cost/latency report — a Final deliverable — aren't produced. |

*(R1/R3 from the 2026-07-15 pass — "hybrid RAG advertised, sparse-only shipped" and "W2 graph is a black box to
telemetry" — are resolved and dropped.)*

---

## 4. Recommendations (prioritized)

**Remaining before Final (the graded W2 core is met):**
1. **NFR-TRACE-W2** — wrap the evidence endpoint in a root `Activity` and open a child span per supervisor
   worker off the existing `AgentForgeActivitySource` (the plumbing exists; the workers just don't call it).
   Confirm the OTel tracer exports. Closes R1 and lets the trace back the cost/latency report.
2. **FR-EVAL RAG coverage** — add a handful of evidence-retrieval / grounding golden cases so the gate guards
   FR-RAG behavior (R2).
3. **NFR-API-W2** — commit the OpenAPI spec + extend the Bruno collection to the W2 endpoints (R4).
4. **NFR-SEC-W2** — apply (or explicitly document the deferral of) at-rest encryption on the `DerivedFact`
   store (R3).
5. **NFR-HEALTH-W2** — add a vector-index/Postgres readiness check to `/ready`.
6. **NFR-PERF-W2 + FR-OBS-W2-3** — capture Week 2 baseline profiles and produce the cost/latency report from
   the now-emitted telemetry (R5).

**Verify (quick confirmations, may already hold):**
7. FR-DOC-5 extractor populates `ExtractionConfidence` for both schemas · NFR-CI-W2
   dependency-audit/security-scan jobs.

---

*Re-audit reflects `develop` as of 2026-07-16 (supersedes 2026-07-15). Status is from code inspection;
"Not fully traced" items need a short confirmation pass. Related: saga #71; closed since last pass — #82
(FR-RAG), #96/#109 (FR-CITE-2), #85/#57 + W2 metrics (FR-OBS). Open tail: NFR-TRACE, NFR-API, NFR-SEC,
NFR-PERF / FR-OBS-W2-3.*
