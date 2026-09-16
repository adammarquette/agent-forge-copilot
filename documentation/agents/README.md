# Agent contracts — index

Every agent contract in this repository, in one place. **Two of them do not live in this folder**, and that is
deliberate: a contract's location decides *when it loads*, so the file sits wherever it must be to arrive at the
right moment.

| Contract | Lives at | Loads |
|---|---|---|
| **Coding Agent** — production code + the test-first unit tests that drive it | [`src/AGENTS.md`](../../src/AGENTS.md) | **automatically**, on your first read of a file in `src/` |
| **Integration Testing Agent** — integration tests against real OpenEMR/MySQL in QA | [`tests/AGENTS.md`](../../tests/AGENTS.md) | **automatically**, in that project |
| **Code Reviewer** — reviewing changes anywhere | [`code-reviewer.md`](code-reviewer.md) | **on demand** — open it when you take the hat. Claude Code: the `code-reviewer` skill, or the [`code-reviewer` subagent](../../.claude/agents/code-reviewer.md) when an author spawns its own reviewer |
| **Platform Agent** — CI, the image, compose, the proxy, deploy | [`platform.md`](platform.md) | **on demand**. Claude Code: the `platform` skill |

Universal rules that bind all four: the root [`AGENTS.md`](../../AGENTS.md). The role model is also recorded in
[`ENGINEERING_STANDARDS.md` §15](../ENGINEERING_STANDARDS.md).

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

## Never mix hats in one pass

If you carry more than one role, run them separately. The Integration Testing Agent writes tests from the
requirement against real dependencies; review reads the implementation against that same requirement. Doing
either pair at once collapses the independence that makes both worth running.

## Not here

There is **no Coordinator contract** — no dispatcher that picks work off a board and drives it to approval. That
role needs a documented board workflow to read from, and this repo has none yet. Add the workflow doc first; a
coordinator written ahead of it would be inventing process.
