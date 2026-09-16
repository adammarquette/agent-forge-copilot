## What & why

<!-- One or two sentences. The "why" matters more than the "what" - the diff shows the what. -->

Closes #

<!-- No orphaned PRs: every PR references a tracking issue opened BEFORE the PR (root AGENTS.md). -->

## Docs

Routed via [`documentation/INDEX.md`](../documentation/INDEX.md) section 1 (doc -> what it owns)
and section 2 (code -> project -> spec):

- [ ] Every doc this change invalidates is updated **in this PR**
- [ ] Adjacent docs checked and deliberately left unchanged:  <!-- list them, or "none" -->
- [ ] `~tok` prices still match (`scripts/check-doc-sizes.sh`) if any priced doc changed size

If genuinely nothing went stale, delete the boxes above and put a line in this body reading
`docs: n/a - <why nothing went stale>` (the `docs-sync` CI job looks for it).

## Verification

- [ ] `dotnet format --verify-no-changes` clean
- [ ] Unit tests green
- [ ] Eval tests / eval gate green
- [ ] Synthetic data only - no real PHI anywhere, including logs and fixtures
