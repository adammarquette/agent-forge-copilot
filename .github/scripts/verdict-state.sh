#!/usr/bin/env bash
# Reads the review verdict for a pull request and says whether it still stands.
#
# This is the single reader. The review-verdict gate, post-verdict.sh's confirmation step and
# watch-verdict.sh all call it, so "what counts as a verdict" is defined in exactly one place -
# a second parser that disagreed would let a PR look ruled to one caller and unruled to another.
#
#   verdict-state.sh <pr>
#   verdict-state.sh --parse   (reads a review body on stdin, prints the verdict; for the self-test)
#
# Prints ONE line:
#   STATE=approved|changes-requested|stale|none  VERDICT=<v>  FRESH=yes|no  COMMIT=<sha>  REVIEW_ID=<id>
#
# CONSUMERS MUST SWITCH ON `STATE`, NEVER ON `VERDICT` ALONE. A GitHub review is permanent, so a
# Request changes stays readable forever; matching VERDICT=request-changes without checking freshness
# makes the gate reject the very push that was meant to clear it, and tells the author to "fix it and
# push" immediately after they did - a livelock (found reviewing gh#400). STATE folds freshness in so
# the mistake is not available to make.
#
# Exit 0 only for STATE=approved. The gate reads the line; scripts read the exit.
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
# merge. A rebase or target-sync keeps a verdict; a new commit or a conflict resolution kills it.
# A FORCE-PUSHED rebase orphans the reviewed commit, so merge-base fails and freshness dies: that
# fails closed (stale, not approved), which is the safe direction.
#
# No python: this runs on runners where only `python3` may exist, and a reader that dies on a
# missing interpreter is indistinguishable from "nobody ruled" (gh#400). gh's own --jq does it all.
set -euo pipefail

verdict_of() {
    line=$(printf '%s\n' "$1" | head -1 | tr -d '\r*_' | sed 's/^[[:space:]]*//' | tr '[:upper:]' '[:lower:]')
    if printf '%s' "$line" | grep -qE '^verdict:[[:space:]]*approve([^a-z]|$)'; then
        echo approve
    elif printf '%s' "$line" | grep -qE '^verdict:[[:space:]]*request changes([^a-z]|$)'; then
        echo request-changes
    else
        echo none
    fi
}

# --parse drives THIS parser from the self-test; a copy that drifted is the whole failure mode.
if [ "${1:-}" = "--parse" ]; then
    verdict_of "$(cat)"
    exit 0
fi

PR="${1:?usage: verdict-state.sh <pr>}"
REPO="${GH_REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"

BASE_REF=$(gh api "repos/$REPO/pulls/$PR" --jq '.base.ref')
HEAD_SHA=$(gh api "repos/$REPO/pulls/$PR" --jq '.head.sha')

contribution() {
    commit="$1"
    mb=$(git merge-base "origin/$BASE_REF" "$commit" 2>/dev/null) || return 1
    git diff "$mb" "$commit" 2>/dev/null | git patch-id --stable | awk '{print $1}'
}

HEAD_ID=$(contribution "$HEAD_SHA" || true)

# --paginate applies --jq PER PAGE, so a `reverse` inside the filter only reverses within a page and
# silently breaks ordering past 30 reviews (gh#400). Emit in API order and reverse the whole stream.
# Body travels base64 so a multi-line review stays one record.
rows=$(gh api "repos/$REPO/pulls/$PR/reviews" --paginate \
    --jq '.[] | [(.id|tostring), (.commit_id // ""), (.body // "" | @base64)] | @tsv' | tac)

V=none; FRESH=no; RID=; RCOMMIT=
while IFS=$'\t' read -r rid rcommit b64; do
    [ -n "${rid:-}" ] || continue
    body=$(printf '%s' "$b64" | base64 -d 2>/dev/null || true)
    v=$(verdict_of "$body")
    [ "$v" = "none" ] && continue
    V="$v"; RID="$rid"; RCOMMIT="$rcommit"
    if [ -n "$rcommit" ] && [ -n "$HEAD_ID" ]; then
        c=$(contribution "$rcommit" || true)
        [ -n "$c" ] && [ "$c" = "$HEAD_ID" ] && FRESH=yes
    fi
    break
done <<< "$rows"

if [ "$V" = "none" ]; then
    STATE=none
elif [ "$FRESH" != "yes" ]; then
    STATE=stale
elif [ "$V" = "approve" ]; then
    STATE=approved
else
    STATE=changes-requested
fi

echo "STATE=$STATE VERDICT=$V FRESH=$FRESH COMMIT=${RCOMMIT:-none} REVIEW_ID=${RID:-none}"
[ "$STATE" = "approved" ]
