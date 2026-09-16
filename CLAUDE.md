# CLAUDE.md

Agent instructions for this repository are maintained in **`AGENTS.md`** (the cross-tool standard), so the same
rules apply whether you're driven by Claude Code, Copilot, or another agent.

**Claude Code:** follow the root `AGENTS.md` (imported below), **plus the nearest `AGENTS.md`** in whatever
subtree you're editing — `src/AGENTS.md` (Coding Agent) or `tests/AGENTS.md` (Integration Testing Agent). Those
directories also carry a `CLAUDE.md` that imports their `AGENTS.md`.

**Role contracts do not auto-load.** Reviewing a change, or touching CI/compose/deploy, means opening
`documentation/agents/code-reviewer.md` or `documentation/agents/platform.md` yourself. The `code-reviewer` and
`platform` skills in `.claude/skills/` exist to trigger that, and `.claude/agents/code-reviewer.md` is the
subagent to spawn instead of reviewing your own work.

@AGENTS.md
