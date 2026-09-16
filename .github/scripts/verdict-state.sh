#!/usr/bin/env bash
# Reads the review verdict for a pull request and says whether it still stands.
#
# This is the single reader. The review-verdict gate, post-verdict.sh's confirmation step and
# watch-verdict.sh all call it, so "what counts as a verdict" is defined in exactly one place -
# a second parser that disagreed would let a PR look ruled to one caller and unruled to another.
#
#   verdict-state.sh <pr>
#
# Prints:  VERDICT=approve|request-changes|none  FRESH=yes|no  COMMIT=<sha>  REVIEW_ID=<id>
# Exits 0 when a FRESH verdict exists, 1 otherwise. The gate reads the line; scripts read the exit.
#
# WHAT COUNTS (documentation/agents/code-reviewer.md):
#   - a REVIEW body, not a PR comment and not an inline comment. Both of those are visible to a
#     human and invisible here, which is worse than silence because it looks like a verdict.
#   - the verdict on the FIRST line: `**Verdict: Approve**` or `**Verdict: Request changes**`.
#     Forgiving about emphasis, casing, a trailing period and trailing prose; strict about the word.
#
# FRESHNESS binds to the PR's own CONTRIBUTION, not the commit id: patch-id over
# merge-base(base, commit)..commit. `develop` requires branches to be up to date, so every merge
# rewrites every open PR's head - binding to the sha would expire every approval on someone else's
# merge. A rebase or target-sync keeps a verdict; a new commit or a conflict resolution kills it,
# because that is content nobody reviewed.
set -euo pipefail

verdict_of() {
    # $1 = review body; echoes approve|request-changes|none
    line=$(printf '%s
' "$1" | head -1 | tr -d '*_' | sed 's/^[[:space:]]*//' | tr '[:upper:]' '[:lower:]')
    if printf '%s' "$line" | grep -qE '^verdict:[[:space:]]*approve([^a-z]|$)'; then
        echo approve
    elif printf '%s' "$line" | grep -qE '^verdict:[[:space:]]*request changes([^a-z]|$)'; then
        echo request-changes
    else
        echo none
    fi
}

# --parse reads a body on stdin and prints the verdict. It exists so the self-test exercises
# THIS parser rather than a copy of it; two parsers that disagree is the whole failure mode.
if [ "${1:-}" = "--parse" ]; then
    verdict_of "$(cat)"
    exit 0
fi

PR="${1:?usage: verdict-state.sh <pr>}"
REPO="${GH_REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"

meta=$(gh api "repos/$REPO/pulls/$PR" --jq '{base: .base.ref, head: .head.sha}')
BASE_REF=$(printf '%s' "$meta" | python -c "import sys,json;print(json.load(sys.stdin)['base'])")
HEAD_SHA=$(printf '%s' "$meta" | python -c "import sys,json;print(json.load(sys.stdin)['head'])")

# The contribution: what this PR adds on top of where it forks from.
contribution() {
    commit="$1"
    mb=$(git merge-base "origin/$BASE_REF" "$commit" 2>/dev/null) || return 1
    git diff "$mb" "$commit" 2>/dev/null | git patch-id --stable | awk '{print $1}'
}

HEAD_ID=$(contribution "$HEAD_SHA" || true)

# Newest first: a later ruling supersedes an earlier one on the same PR.
rows=$(gh api "repos/$REPO/pulls/$PR/reviews" --paginate --jq 'reverse | .[] | @base64')

BEST_V=none; BEST_FRESH=no; BEST_ID=; BEST_COMMIT=
for row in $rows; do
    decoded=$(printf '%s' "$row" | base64 -d)
    body=$(printf '%s' "$decoded" | python -c "import sys,json;print(json.load(sys.stdin).get('body') or '')")
    rid=$(printf '%s' "$decoded" | python -c "import sys,json;print(json.load(sys.stdin).get('id') or '')")
    rcommit=$(printf '%s' "$decoded" | python -c "import sys,json;print(json.load(sys.stdin).get('commit_id') or '')")
    v=$(verdict_of "$body")
    [ "$v" = "none" ] && continue

    fresh=no
    if [ -n "$rcommit" ] && [ -n "$HEAD_ID" ]; then
        rid_contrib=$(contribution "$rcommit" || true)
        [ -n "$rid_contrib" ] && [ "$rid_contrib" = "$HEAD_ID" ] && fresh=yes
    fi

    BEST_V="$v"; BEST_FRESH="$fresh"; BEST_ID="$rid"; BEST_COMMIT="$rcommit"
    break
done

echo "VERDICT=$BEST_V FRESH=$BEST_FRESH COMMIT=${BEST_COMMIT:-none} REVIEW_ID=${BEST_ID:-none}"
[ "$BEST_V" != "none" ] && [ "$BEST_FRESH" = "yes" ]
