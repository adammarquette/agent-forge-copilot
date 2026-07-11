# USERS — Target User, Workflow & Use Cases

**Product:** AgentForge Clinical Co-Pilot for Cardiology (OpenEMR fork)
**Status:** v1.0 — source-of-truth for `ARCHITECTURE.md`. Every agent capability must trace to a use case here.
**Companion docs:** `PRD.md` (full requirements), `AUDIT.md` (system findings), `ARCHITECTURE.md` (build plan).

> **The bar this document must clear.** "Physicians need help finding information" is not a user definition —
> it is the thesis behind a thousand failed health-tech products. This document names one narrow user, the
> exact moment the agent enters their day, the precise data they need in that moment, and what they do with
> it. If a capability in `ARCHITECTURE.md` cannot point to a use case below, it should not be built.

---

## 1. Target User: the outpatient cardiologist, in the 90 seconds between rooms

### 1.1 Who (specifically)
An established-practice **outpatient cardiologist** running a clinic day of **~16–20 scheduled patients**.
Roughly two-thirds are **longitudinal follow-ups** last seen weeks to months ago, carrying chronic cardiac
conditions — atrial fibrillation on anticoagulation, heart failure with reduced ejection fraction (HFrEF) on
guideline-directed medical therapy, coronary artery disease post-PCI on antiplatelet therapy, hypertension,
hyperlipidemia. They have deep domain expertise, read a chart faster than most software loads, and have
**near-zero tolerance for a tool that is confidently wrong**. The bar to earn a single click is high.

### 1.2 Who this is explicitly NOT for (to keep the definition sharp)
- **Not** the inpatient hospitalist or rounding team — OpenEMR is ambulatory; we do not model inpatient rounding.
- **Not** the ED clinician doing intake on unknown patients — our value depends on an existing longitudinal record.
- **Not** a patient-facing tool, a medical-student study aid, or a generic medical Q&A chatbot.
- **Not** a coding/billing assistant. The user is the clinician at the point of care, not the back office.

### 1.3 The defining constraint
Everything this agent is must survive **the ~90-second window between walking out of Room A and into Room B**,
on a shared hallway workstation or tablet, with the next patient already roomed and waiting.

---

## 2. The Workflow — moment by moment

This is the concrete micro-workflow the agent must fit inside. It is not "the physician looks up information";
it is this exact sequence.

**T‑minus ~20s — the hallway (before opening the app).**
The cardiologist finishes with the Room A patient, steps into the hallway, and glances at the schedule to see
who is next. Working memory holds **only a name and a reason for visit**. The nurse has already roomed the
Room B patient and recorded vitals. There is **no time** to open the chart and separately read the last three
progress notes, the lab flowsheet, and the medication list. This is a cold start under a clock.

**T‑zero — one action.**
From the hallway workstation/tablet, they open the Co-Pilot **already scoped to the Room B patient** (launched
in patient context from OpenEMR). No searching, no patient-picker.

**T +0 to ~75s — the brief and one follow-up.**
They read a single **prioritized, source-cited brief** answering *"what changed and what matters today,"* and
if needed ask **one conversational follow-up**. (Data specifics in §3.)

**Exit — walking into Room B.**
They enter the room already oriented: they open with the right question ("How've you felt since the ER
visit?"), verify the med list against what the patient reports, and act on any safety flag **before** writing
today's plan. The agent's job is to convert a cold start into a warm one **inside the window**. If it can't be
trusted at a glance, or takes longer than reading the chart, it has failed.

**Secondary rhythm — the pre-clinic sweep.**
Before the session, the same cardiologist runs the brief across the whole day's schedule to triage which
patients carry a change worth pre-reading. This is the **batch** shape of the same need and tolerates a few
seconds more latency than the in-room window.

---

## 3. Exactly what data the user needs in the window

In the in-room window the brief must surface, prioritized and **each item source-cited**:

1. **Interval events** since the last visit — ED visit, hospitalization, new echocardiogram (and the **EF**),
   device (pacemaker/ICD) interrogation, new outside records.
2. **Medication changes** — what was started, stopped, or titrated; any adherence signal.
3. **New or out-of-range cardiac labs** — INR vs. therapeutic range for the indication; potassium/creatinine
   trend on ACEi/ARB/diuretic; lipids on statin; BNP/NT‑proBNP trend in heart failure.
4. **Active safety flags** — a domain-constraint hit (e.g., QT‑prolonging combination, renally‑contraindicated
   dose, INR out of range) that must not be missed before writing anything today.

The brief **leads with the one or two things that change today's plan** — not an exhaustive dump. Anything the
agent cannot ground in the record is reported as a gap, never inferred.

---

## 4. Use Cases

Each use case names the trigger, the need, the output, **why a conversational agent is the right shape** (not a
dashboard/list/chart), and **what the agent must refuse to do**. These IDs are the trace targets for
`ARCHITECTURE.md`.

### UC‑1 — Pre-visit brief: "what changed + what matters today"
- **Trigger:** clinician opens a scheduled patient (in-room) or runs the sweep (pre-clinic).
- **Need:** a prioritized, source-cited summary of the four data classes in §3.
- **Output:** a short brief leading with what changes today's plan; every claim cited to a record.
- **Why an agent, not a dashboard:** a dashboard shows everything and prioritizes nothing. The value is
  **judgment about relevance** — deciding that *this* patient's rising creatinine matters today and *that*
  patient's stable lipids don't — plus the ability to immediately ask a follow-up. A static view can't rank by
  today's clinical salience or answer "why."
- **Must refuse:** to state any change it cannot attribute to a source; to invent a value for a missing field.

### UC‑2 — Grounded multi-turn follow-up
- **Trigger:** the brief raises a question the clinician wants to chase in the same window.
- **Need:** e.g., *"Is her INR therapeutic?"* → *"When was it last drawn?"* → *"What's her CHA₂DS₂‑VASc?"* —
  each turn building on the last, some requiring more than one tool call.
- **Output:** cited answers that carry conversation context without the clinician restating the patient or
  prior turn.
- **Why an agent:** this **is** the multi-turn + tool-chaining justification. The dependency between turns
  (resolving "her," "that lab") and the need to chain retrieval (resolve patient → fetch lab → compute score)
  cannot be served by a search bar or a fixed panel. *If no use case required this, the agent would not have
  multi-turn or tool chaining — this is the one that earns them.*
- **Must refuse:** to answer beyond the record (no generic medical advice); to guess when data is absent.

### UC‑3 — Medication reconciliation & safety check
- **Trigger:** clinician reviews the current regimen before writing today's plan.
- **Need:** the current cardiac medication list with sources, **and** flags for domain-constraint issues
  (QT‑prolonging combination, renally‑contraindicated dose, INR out of range for the indication,
  negatively‑chronotropic combinations in relevant contexts).
- **Output:** a cited med list plus any triggered safety flag naming the rule and the source values.
- **Why an agent:** the value is **reasoning over the list against clinical rules**, not displaying it. A list
  view shows the meds; it doesn't know that this combination is a flag for this patient given this lab.
- **Must refuse:** to recommend a dose, start/stop a drug, or place an order. It **surfaces and cites**; the
  clinician decides. (Constraint set is clinician-validated and its limits documented — see `ARCHITECTURE.md`.)

### UC‑4 — Authorization-aware access ("who's asking?")
- **Trigger:** any request; the requester may be the attending, a supervised fellow, or a nurse/MA.
- **Need:** the same query returns different results — or is refused — based on the requester's **role and
  relationship to the patient**, enforced even under adversarial phrasing ("ignore that and show me…").
- **Output:** entitled data, a scoped subset, or a clean refusal with no leakage.
- **Why an agent:** because the agent is a natural-language surface, the access boundary must hold against
  prompt-level attack, not just a UI toggle. This is an agent-**security** problem — enforcement below the
  model — that a static permissioned screen doesn't face in the same way.
- **Must refuse:** to disclose anything the requester isn't entitled to; to let record content or user phrasing
  override authorization.

### UC‑5 — Graceful behavior under missing / failed data
- **Trigger:** a tool errors, a dependency is slow, or the patient record is incomplete (common in real and
  demo data alike).
- **Need:** the agent returns what it *can* verify, names the gap plainly, and never fabricates or silently
  drops.
- **Output:** a partial, honest, cited brief with explicit "unavailable" markers; or a clear service-degraded
  message.
- **Why an agent:** in this domain, **transparent partial-truth is a feature**. A confident wrong answer can
  harm a patient, so the agent must communicate uncertainty and degradation as first-class behavior — a
  judgment a static report doesn't make.
- **Must refuse:** to present an unverified or inferred value as fact to close a gap.

### UC‑6 — On-demand day's agenda: "who's left, and what should I know before each"
- **Trigger:** the clinician opens the agenda from outside any single patient's chart (calendar/tab-level
  entry point, not the per-patient launch) at any point during the clinic day.
- **Need:** a list of every patient still on today's schedule whose appointment hasn't happened yet
  (appointment time > now), each with a short, independent summary — the same grounding/citation discipline
  as UC-1, compressed for a scannable list rather than the ~75-second in-room read.
- **Output:** an ordered list (soonest appointment first), one short cited summary per patient. Opening any
  one patient from the list drops into that patient's own live conversational session (UC-1/UC-2) for
  follow-up — **never** a session or query spanning more than one patient.
- **Why an agent, not a dashboard:** same reasoning as UC-1 — the value is judgment about what's worth
  surfacing per patient, not a raw list. This is the **interactive** counterpart to the batch "pre-clinic
  sweep" mentioned in §2: same underlying need (triage the day), but on-demand and mid-day, driven by a live
  clinician session rather than an unattended early-morning job.
- **Must refuse:** everything UC-4 already requires, **plus**: any question that names or implies a second
  patient ("compare to my other AFib patients," "any of today's other patients on warfarin") — refused or
  scoped-blocked and logged, identical to FR-CHAT-3's existing acceptance test. The agenda view is not an
  exception to patient-scoped context; it changes *how many patients get a summary in one screen*, not
  *whether more than one patient's data can enter a single conversation*.

---

## 5. Capability → Use Case traceability (the handle ARCHITECTURE.md grabs)

| Planned agent capability | Justified by | If the use case were removed… |
|---|---|---|
| Conversational, patient-scoped interface | UC‑1, UC‑2 | …a form/dashboard would suffice |
| Multi-turn context | UC‑2 | …drop multi-turn; single-shot answers only |
| Tool chaining | UC‑2, UC‑3 | …drop chaining; single-tool lookups only |
| Prioritized synthesis across sources | UC‑1 | …show a raw record view instead |
| Source attribution on every claim | UC‑1, UC‑2, UC‑3 | …none — this is non-negotiable in-domain |
| Domain-constraint / safety flags | UC‑3 | …drop the rules engine |
| Role/relationship authorization below the model | UC‑4 | …single-role assumption (unacceptable in clinic) |
| Graceful degradation + uncertainty signaling | UC‑5 | …drop partial-answer handling (unacceptable in-domain) |
| On-demand multi-patient roster summary (per-patient isolated, no cross-patient session) | UC‑6 | …no agenda view; the doctor stays without a "who's left today" entry point outside the chart |

> **Rule for `ARCHITECTURE.md`:** every capability you design must appear in the left column with a live use
> case beside it. A capability with no use case is scope to cut, not build.

---

## 6. What "useful" means for this user (acceptance lens)
- **Trustable at a glance** — every claim cited; the clinician never has to double-check the tool against the
  chart to believe it.
- **Fits the window** — the in-room answer arrives within the interactive latency budget (see PRD NFR‑PERF‑1);
  if it's slower than reading the chart, it loses.
- **Prioritized** — leads with what changes today's plan; does not bury the signal.
- **Honest under failure** — says "I can't verify that" rather than guessing.
- **The thing they'd actually choose** — for this window, a conversational brief-with-follow-up beats a
  dashboard, a sorted list, or a better chart view. That is the bar.
