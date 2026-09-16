#!/usr/bin/env bash
# Blocks until a pull request has a verdict. This is what the AUTHORING agent waits on.
#
#   watch-verdict.sh verdict <pr> [timeout-seconds]
#
# Exit 0  - a fresh Approve stands; the PR is reviewed
# Exit 1  - Request changes; go fix it, then push (only a push clears it)
# Exit 2  - no verdict before the deadline; the reviewer never ruled, or ruled somewhere the gate
#           cannot read. Do NOT treat this as approval, and do not merge around it.
#
# Why block at all: a review that arrives after the authoring session is gone lands in an empty
# room, and gets addressed by a session that has to rebuild the reasoning from the diff. Waiting
# is cheaper than re-deriving.
#
# This reads through verdict-state.sh - the same reader the CI gate uses - so "reviewed" means
# exactly one thing here and in CI.
set -uo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
CMD="${1:?usage: watch-verdict.sh verdict <pr> [timeout-seconds]}"
PR="${2:?a pull request number is required}"
TIMEOUT="${3:-1800}"
[ "$CMD" = "verdict" ] || { echo "watch-verdict: unknown command '$CMD'" >&2; exit 2; }

DEADLINE=$(( $(date +%s) + TIMEOUT ))
echo "watch-verdict: waiting up to $((TIMEOUT / 60))m for a verdict on PR #$PR"

while :; do
    out=$(bash "$here/verdict-state.sh" "$PR" 2>&1) && rc=0 || rc=$?
    case "$out" in
        *VERDICT=request-changes*)
            echo "watch-verdict: CHANGES REQUESTED - $out"
            echo "watch-verdict: read the review on the PR, fix it, and push. Only a push clears this."
            exit 1 ;;
    esac
    if [ "$rc" -eq 0 ]; then
        echo "watch-verdict: APPROVED - $out"
        exit 0
    fi
    if [ "$(date +%s)" -ge "$DEADLINE" ]; then
        echo "watch-verdict: NO VERDICT after $((TIMEOUT / 60))m - $out" >&2
        echo "watch-verdict: the reviewer never ruled, or ruled where the gate cannot read it" >&2
        echo "watch-verdict: (a PR comment and an inline comment both look right and do not count)" >&2
        exit 2
    fi
    sleep 30
done
