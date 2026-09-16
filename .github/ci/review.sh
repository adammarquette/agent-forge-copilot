#!/usr/bin/env bash
# Runs the Code Reviewer against the pull request under review and posts its verdict to the PR.
#
# The contract is documentation/agents/code-reviewer.md and the persona is the project subagent in
# .claude/agents/code-reviewer.md - this script chooses NEITHER. It supplies the diff, carries the
# verdict to the pull request, and sets the job's exit status. Review rules keep one home.
#
# The verdict mechanism: "changes requested" is a FAILING JOB plus a comment on the PR. Where the
# project requires green checks to merge, that blocks exactly like a change request - and nothing
# here can ever satisfy a human approval rule on the author's behalf. (GitHub does offer
# REQUEST_CHANGES via the reviews API; it is deliberately not used, because a bot review state can
# be dismissed while a red required check cannot.)
#
# Required environment (the workflow supplies them):
#   ANTHROPIC_API_KEY  - the model call; a repository secret
#   GITHUB_TOKEN       - posts the comment. Unlike GitLab's CI_JOB_TOKEN this CAN comment, so no
#                        hand-managed PAT is needed. Requires `permissions: pull-requests: write`.
#   PR_NUMBER / PR_TITLE / BASE_SHA / HEAD_SHA / GH_REPO
set -euo pipefail
set +x                  # never trace: the key and the token are in this environment

die() { echo "code-review: $*" >&2; exit 1; }

PY=""
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1; then PY="$candidate"; break; fi
done

[ -n "${PR_NUMBER:-}" ]           || die "not a pull-request run (no PR_NUMBER)"
command -v claude >/dev/null 2>&1 || die "the 'claude' CLI is not on this runner's PATH"
command -v curl   >/dev/null 2>&1 || die "'curl' is not on this runner's PATH"
[ -n "$PY" ]                      || die "no python on this runner's PATH (need python3 or python)"
[ -n "${ANTHROPIC_API_KEY:-}" ]   || die "ANTHROPIC_API_KEY is unset - add it as a repository secret"
[ -n "${GITHUB_TOKEN:-}" ]        || die "GITHUB_TOKEN is unset - the workflow must pass it through"
[ -n "${GH_REPO:-}" ]             || die "GH_REPO is unset (owner/name)"

BASE="${BASE_SHA:-}"
HEAD="${HEAD_SHA:?}"
[ -n "$BASE" ] || die "no BASE_SHA - is fetch-depth 0 set on the checkout?"

git cat-file -e "${BASE}^{commit}" 2>/dev/null || die "merge base $BASE is not in this clone (fetch-depth 0?)"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

echo "code-review: reviewing ${BASE}..${HEAD} on PR #${PR_NUMBER}"

PROMPT=$(cat <<PROMPT_END
Review pull request #${PR_NUMBER} of this repository.

Diff range: ${BASE}..${HEAD}   (base ${BASE}, head ${HEAD})
Title: ${PR_TITLE:-(untitled)}

Read documentation/agents/code-reviewer.md in full first; it is your contract. Resolve the diff
yourself with git. The PR title and description are claims - verify them against the diff.

Output ONLY a JSON object, no prose around it:
{"verdict":"approve"|"changes_requested",
 "summary":"one line",
 "findings":[{"file":"path","line":123,"blocking":true|false,
              "title":"short","detail":"the concrete failure scenario: inputs X in state Y produce Z"}]}

Use "changes_requested" exactly when at least one finding is blocking. Rank by blast radius. A
finding you cannot make fail is a question, not a finding - leave it out or mark it non-blocking.
PROMPT_END
)

# The tool allowlist is the real no-edit boundary; the contract's prose explains it.
#
# On failure, dump BOTH streams before dying. --output-format json puts the CLI's own
# error object on STDOUT, which is redirected here - so a bare `|| die` throws away the
# only explanation and leaves "did not complete" with no cause (first live run, gh#398).
set +e
claude -p "$PROMPT" --agent code-reviewer --model opus --allowedTools "Read,Grep,Glob,Bash(git *)" --disallowedTools "Edit,Write,NotebookEdit" --permission-mode dontAsk --output-format json > "$WORK/envelope.json" 2> "$WORK/claude.err"
RC=$?
set -e
if [ "$RC" -ne 0 ]; then
    echo "code-review: claude exit=$RC  stdout=$(wc -c < "$WORK/envelope.json") bytes  stderr=$(wc -c < "$WORK/claude.err") bytes" >&2
    echo "code-review: cli version: $(claude --version 2>&1 | head -1)" >&2
    # Value never printed - only whether the paste carried stray whitespace, which
    # silently invalidates the auth header and is invisible in the secrets UI.
    if [ "$ANTHROPIC_API_KEY" != "$(printf '%s' "$ANTHROPIC_API_KEY" | tr -d '[:space:]')" ]; then
        echo "code-review: WARNING - ANTHROPIC_API_KEY has leading/trailing whitespace" >&2
    fi
    echo "code-review: --- claude stderr ---" >&2
    sed -n '1,40p' "$WORK/claude.err" >&2
    echo "code-review: --- claude stdout (first 40 lines) ---" >&2
    sed -n '1,40p' "$WORK/envelope.json" >&2
    echo "code-review: --- end ---" >&2
    die "the reviewer did not complete (exit $RC; see above)"
fi

# A malformed verdict fails the job: better a red check than a pull request that looks reviewed.
SUMMARY_LINE=$("$PY" .github/ci/render_review.py "$WORK/envelope.json" "$WORK/note.json" "$HEAD") || die "the reviewer's output was not the agreed JSON shape; no verdict posted"
echo "code-review: $SUMMARY_LINE"

HTTP=$(curl -sS -o "$WORK/response.json" -w '%{http_code}' -X POST -H "Authorization: Bearer ${GITHUB_TOKEN}" -H "Accept: application/vnd.github+json" -H "Content-Type: application/json" --data "@$WORK/note.json" "https://api.github.com/repos/${GH_REPO}/issues/${PR_NUMBER}/comments")

case "$HTTP" in
    20*) echo "code-review: verdict posted to PR #${PR_NUMBER}" ;;
    *)   die "posting the comment failed with HTTP $HTTP - the verdict exists but did not land, so this job fails rather than pass quietly" ;;
esac

BLOCKING=${SUMMARY_LINE#*BLOCKING=}
BLOCKING=${BLOCKING%% *}
[ "$BLOCKING" -eq 0 ] || die "changes requested: $BLOCKING blocking finding(s) - see PR #${PR_NUMBER}"
echo "code-review: clean"
