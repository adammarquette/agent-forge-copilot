# Task sizing — choosing the cheapest model that can do the job

A shared rubric, not a contract: every role uses it, none of them owns it. It answers one question —
**what is the smallest model that can finish this task correctly** — so a docstring fix does not cost what a
retrieval-pipeline change costs.

Size the **task**, not the diff you hope to write. "Probably two lines" is a prediction; the tier comes from
what the task *touches* and what it would cost to be wrong.

## The tiers

| Tier | Looks like | Model | Effort |
|---|---|---|---|
| **XS** | Typo, comment wording, a doc price, a version bump with no behavior change. One file, no logic. | Haiku | low |
| **S** | A localized change inside one project with the test already describing it. No new public surface, no cross-project reasoning. | Haiku or Sonnet | low–medium |
| **M** | A feature or fix inside one project that needs a new test, touches a public method, or requires reading two or three files to be safe. | Sonnet | medium |
| **L** | Crosses project boundaries, changes a contract or an interface, needs repo-wide context, or is a debugging task with no known cause. | Opus | high |
| **Floor** | Anything in the list below, **at any size**. | Opus | high |

## The floor — category beats size

**These always take the strongest model, even as a one-line change**, because the size signals are exactly
wrong here: the smallest diffs on these paths are the ones that end up in an incident note.

- **Clinical output.** Generation, summarization, or anything that produces text a cardiologist reads.
- **Citation and grounding.** The evidence path, bounding boxes, `patient/Binary.read`, anything that decides
  whether a claim can be traced to a source.
- **Degrade and refusal paths.** `ARCHITECTURE.md` §13.1 — the system must degrade deterministically, and the
  failure mode of a cheap edit here is a plausible answer with no citation, which reads as success.
- **Authorization, scopes and token custody.** Anything near SMART launch, `finalizeScopes`, introspection, or
  a token's lifetime.
- **PHI in logs or telemetry.** Adding or changing what gets logged, traced, or exported.
- **Tool I/O contracts** (NFR-CONTRACT-1) and anything in `INTERFACE_CONTROL.md`.
- **The eval gate itself.** A change to what the gate measures cannot be graded by the gate.
- **A review verdict.** Reviewing is judgment under uncertainty, and a cheap reviewer fails silently — it
  produces confident, well-formatted findings that are not the real ones.

## Applying it

1. **Name the tier before dispatching**, in the same sentence as the task. "S — add the missing null guard in
   `OpenEmrAuthClient`, test exists."
2. **Ties round up.** Between two tiers, take the higher one. The saving from guessing low once does not cover
   one wrong answer on a clinical path.
3. **Escalate rather than retry.** If a tier fails twice — gates red twice, or the same misunderstanding twice
   — move up a tier instead of re-prompting. Two failed attempts at a tier is the signal that the sizing was
   wrong, not that the prompt was.
4. **Unknown cause means L.** A bug with no diagnosis is not XS because the fix might be one line. Size the
   *diagnosis*.
5. **Sizing is not a permission.** A cheap tier still obeys every contract; it does not get to skip the
   test-first rule or the docs-in-lockstep rule because it is small.

## What this does not decide

Model choice, not scope. It never licenses a smaller change than the task needs, and it never overrides a role
contract — [`code-reviewer.md`](code-reviewer.md) still governs review no matter which model runs it.
