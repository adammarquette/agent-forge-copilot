# Agent contracts — index

Every agent contract in this repository, in one place. **Two of them do not live in this folder**, and that is
deliberate: a contract's location decides *when it loads*, so the file sits wherever it must be to arrive at the
right moment.

## The contracts

`~tok` is what the read costs — bytes ÷ 4, to 0.1K — so you can see the price before paying it.
`scripts/check-doc-sizes.sh` fails CI when a number here stops matching its file.

| Contract | Lives at | ~tok | Loads |
|---|---|---|---|
| **Coding Agent** — production code + the test-first unit tests that drive it | [`src/AGENTS.md`](../../src/AGENTS.md) | 1.5K | **automatically**, on your first read of a file in `src/` |
| **Integration Testing Agent** — integration tests against real OpenEMR/MySQL in QA | [`tests/AGENTS.md`](../../tests/AGENTS.md) | 0.7K | **automatically**, in that project |
| **Code Reviewer** — reviewing changes anywhere; **reads and reports only, never edits**, and leaves an approve / request-changes verdict on the pull request | [`code-reviewer.md`](code-reviewer.md) | 3.5K | **on demand** — open it when you take the hat. Claude Code: the `code-reviewer` skill, or the [`code-reviewer` subagent](../../.claude/agents/code-reviewer.md) when an author spawns its own reviewer |
| **Platform Agent** — CI, the image, compose, the proxy, deploy | [`platform.md`](platform.md) | 1.4K | **on demand**. Claude Code: the `platform` skill |
| **Coordinator** — drives an MR from changes-requested back to green; **dispatches, never writes the fix, never merges or approves** | [`coordinator.md`](coordinator.md) | 1.3K | **on demand**. Claude Code: the `coordinator` skill |
| *(shared rubric, not a contract)* — which model tier a task takes, and the categories that always take the strongest one | [`task-sizing.md`](task-sizing.md) | 0.9K | read it when dispatching or choosing a model |

Universal rules that bind all four: the root [`AGENTS.md`](../../AGENTS.md). **This file is the role model's
one home** — [`ENGINEERING_STANDARDS.md` §15](../ENGINEERING_STANDARDS.md) keeps only what is a *standard*
rather than a routing fact: the `CLAUDE.md` shim bridge, and the rule that when an `AGENTS.md` and a doc
disagree, the doc wins.

## Why they are not all in this folder

`AGENTS.md` and `CLAUDE.md` **auto-load by directory proximity**; a file named for its role never does. So:

- **Subtree-scoped** contracts (Coding, Integration Testing) are already in the right place — proximity delivers
  them exactly when they apply, and moving them here would mean an agent writing C# no longer receives the
  coding standards unprompted. It would also break `AGENTS.md`-by-directory discovery for other tools, which is
  why the repo standardised on `AGENTS.md` over `CLAUDE.md` in the first place.
- **Role-scoped** contracts (Reviewer, Platform) follow *what you are doing*, not where a file sits. Filing them
  under a directory would load them for whoever edited that directory — never the person in the role — so they
  live here and are opened deliberately.

The rule, in one line: **put a contract where it must be to load when it applies** — and catalogue them all here.

**The cost of that design is that they will not arrive on their own.** Wearing one of those hats without opening
its contract is the easiest way to get this repo wrong, and it is the failure nothing catches: no check fails, no
reviewer sees a diff, the work is simply done without it.

## Skills narrow that gap; they do not close it

Each role contract has a Claude Code **skill** in [`.claude/skills/`](../../.claude/skills/) whose `description`
names the work that should trigger it, so the contract can arrive on intent rather than on the agent remembering.
The Code Reviewer additionally exists as a **[subagent](../../.claude/agents/code-reviewer.md)**, because *never
mix hats* is a statement about context: a skill loads into the caller's, while a subagent runs in one that never
saw the change being written — which is what an author spawning its own reviewer needs.

**A skill is a trigger, not a contract.** Every one is a pointer to the file in this folder and restates none of
it, so a rule keeps one home and deleting a skill loses the prompt rather than the rule. Two things it does not
fix: the trigger is a *match*, not a guarantee, and other tools read `AGENTS.md` and see no skills at all.
Opening the contract yourself remains the thing you are accountable for.

**Neither route starts on its own, and nothing schedules one.** The CI job that used to review every push was
deleted and is not coming back ([`CI-SETUP.md`](../CI-SETUP.md) §7): the authoring agent spawns this contract
itself and blocks on its verdict (`src/AGENTS.md`). What needs no one to remember it is the **gate** —
`review-verdict` runs on every pull request and waits for a ruling, so a PR nobody reviewed stalls instead of
merging quietly (§8). Post the verdict as a **review body**: a PR comment or an inline note is plainly visible
to a human and invisible to the gate, which leaves the author's watcher waiting out its deadline next to a
ruling that does not count. That is the floor, and it is a floor about *whether* anyone ruled — it does not
retire the hat, because only a reviewer asks whether the change should exist at all.

## The reviewer is the one role that is also a boundary

The other three contracts describe how to do work. The Code Reviewer's also says what it may not touch: **it
never edits a file**, and its output is a verdict on a pull request — approve, or request changes with findings
attached. That is why it is the one role with a **subagent** as well as a skill: a skill loads into the caller's
context, so an author who invokes it is still the author, holding both hats and reviewing a diff they are still
free to change. The subagent runs in a context that never saw the change and has **no file-editing tools at
all**, which makes the boundary structural instead of a promise. Spawn it rather than reviewing your own work.

A reviewer that edits collapses three things at once: the author never learns the pattern, the next pass has the
reviewer reviewing their own work, and the diff that was approved is not the diff that landed.

## Never mix hats in one pass

If you carry more than one role, run them separately. The Integration Testing Agent writes tests from the
requirement against real dependencies; review reads the implementation against that same requirement. Doing
either pair at once collapses the independence that makes both worth running.

## The Coordinator's precondition, and how it was met

This folder carried a note saying a Coordinator could **not** be written yet, because the role needs a
documented workflow to read from and the repo had none — a coordinator written ahead of one would be inventing
process. That was the right call, and it is what [`MR_WORKFLOW.md`](../MR_WORKFLOW.md) now fixes: the states an
MR moves through and who acts at each. The contract drives that document rather than a process of its own
invention, which is why the two landed together.

**It still holds the line the review loop depends on.** The Coordinator dispatches the fix and never writes it,
acts on findings and never decides which are real, and stops at green — a human merges. Automate the shepherding,
not the judgement.
