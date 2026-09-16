#!/usr/bin/env bash
# Posts a reviewer's verdict to a pull request, and proves it landed somewhere the gate can read.
#
#   post-verdict.sh preflight <pr>
#   post-verdict.sh review    <pr> <COMMENT|REQUEST_CHANGES> <body-file>
#
# EXIT 0 FROM `review` IS THE ONLY OUTCOME THAT MEANS YOU RULED. Anything else means the verdict
# does not exist as far as the gate is concerned, however good it looks on the PR page.
#
# The near-misses this exists to prevent - all of them visible to a human and invisible to the gate:
#   - a PR COMMENT holding the verdict line: a different endpoint, never read
#   - an INLINE comment: creates a review whose body is EMPTY
#   - a verdict handed back to whoever spawned you: the reviewed party then decides what the review
#     said, and the PR - the durable record the gate reads - never sees it
# Each leaves the author's watcher waiting out its deadline next to a ruling that does not count,
# which is worse than silence, because it looks like a verdict.
#
# A successful post comes back as state COMMENTED. That is the expected shape, not a misfire:
# APPROVE is deliberately not used, because a bot approval can be dismissed and because GitHub
# refuses it on your own PR - the verdict LINE is what counts, not the review state.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
die() { echo "post-verdict: $*" >&2; exit 1; }

CMD="${1:?usage: post-verdict.sh preflight <pr> | review <pr> <STATE> <body-file>}"
PR="${2:?a pull request number is required}"
REPO="${GH_REPO:-$(gh repo view --json nameWithOwner --jq .nameWithOwner)}"

command -v gh >/dev/null 2>&1 || die "the 'gh' CLI is not on PATH - you cannot rule from this session"

case "$CMD" in
preflight)
    gh auth status >/dev/null 2>&1 || die "gh is not authenticated - you cannot rule from this session"
    gh api "repos/$REPO/pulls/$PR" --jq '.number' >/dev/null 2>&1 || die "cannot read PR #$PR in $REPO"
    me=$(gh api user --jq '.login' 2>/dev/null || echo unknown)
    author=$(gh api "repos/$REPO/pulls/$PR" --jq '.user.login')
    echo "post-verdict: ready to rule on $REPO#$PR as '$me' (PR author: '$author')"
    if [ "$me" = "$author" ]; then
        echo "post-verdict: note - you share an identity with the author, so GitHub will refuse an"
        echo "post-verdict:        APPROVE state. This is why the verdict is a LINE, not a state."
    fi
    ;;
review)
    STATE="${3:?COMMENT or REQUEST_CHANGES}"
    BODY_FILE="${4:?a body file is required}"
    [ -f "$BODY_FILE" ] || die "body file not found: $BODY_FILE"
    [ -s "$BODY_FILE" ] || die "body file is empty - an empty review body is one of the near-misses"

    case "$STATE" in
        COMMENT|REQUEST_CHANGES) ;;
        APPROVE) die "APPROVE is not used here; post COMMENT and let the verdict line carry the ruling" ;;
        *) die "unknown state '$STATE' (COMMENT or REQUEST_CHANGES)" ;;
    esac

    parsed=$(bash "$here/verdict-state.sh" --parse < "$BODY_FILE")
    [ "$parsed" != "none" ] || die "the body's first line is not a verdict the gate can read; see documentation/agents/code-reviewer.md"

    gh api "repos/$REPO/pulls/$PR/reviews" -X POST -f "event=$STATE" -F "body=@$BODY_FILE" --jq '.id' >/dev/null || die "posting the review failed - you have NOT ruled"

    # Confirm with the gate's own reader rather than trusting the POST's 200.
    if out=$(GH_REPO="$REPO" bash "$here/verdict-state.sh" "$PR"); then
        echo "post-verdict: ruled. $out"
    else
        echo "post-verdict: posted, but the gate cannot read it: ${out:-no verdict found}" >&2
        die "you have NOT ruled - do not report a verdict the gate does not see"
    fi
    ;;
*)
    die "unknown command '$CMD'"
    ;;
esac
