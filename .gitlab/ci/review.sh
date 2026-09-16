#!/usr/bin/env bash
# Runs the Code Reviewer against the MR under review and posts its verdict to the MR.
#
# The contract is documentation/agents/code-reviewer.md and the persona is the project subagent in
# .claude/agents/code-reviewer.md - this script chooses NEITHER. It supplies the diff, carries the
# verdict to the merge request, and sets the job's exit status. Review rules keep one home.
#
# The verdict mechanism is deliberate: GitLab has no REST endpoint for "request changes" (only
# approve/unapprove), so "changes requested" is a FAILING JOB plus a note on the MR. Where the
# project requires a green pipeline to merge, that blocks exactly like a change request - and
# nothing here can ever satisfy a human approval rule on the author's behalf.
#
# Required CI variables (Settings > CI/CD > Variables, both masked):
#   ANTHROPIC_API_KEY  - the model call
#   REVIEW_BOT_TOKEN   - GitLab PAT / project access token with `api` scope. CI_JOB_TOKEN CANNOT
#                        create notes, so this is neither optional nor substitutable.
set -euo pipefail
set +x                  # never trace: the key and the token are in this environment

die() { echo "code-review: $*" >&2; exit 1; }

PY=""
for candidate in python3 python; do
    if command -v "$candidate" >/dev/null 2>&1; then PY="$candidate"; break; fi
done

[ -n "${CI_MERGE_REQUEST_IID:-}" ] || die "not an MR pipeline (no CI_MERGE_REQUEST_IID)"
command -v claude >/dev/null 2>&1 || die "the 'claude' CLI is not on this runner's PATH"
command -v curl   >/dev/null 2>&1 || die "'curl' is not on this runner's PATH"
[ -n "$PY" ]                      || die "no python on this runner's PATH (need python3 or python)"
[ -n "${ANTHROPIC_API_KEY:-}" ]   || die "ANTHROPIC_API_KEY is unset - add it as a masked CI variable"
[ -n "${REVIEW_BOT_TOKEN:-}" ]    || die "REVIEW_BOT_TOKEN is unset - a PAT with api scope; CI_JOB_TOKEN cannot post notes"

BASE="${CI_MERGE_REQUEST_DIFF_BASE_SHA:-}"
HEAD="${CI_COMMIT_SHA:?}"
[ -n "$BASE" ] || die "no CI_MERGE_REQUEST_DIFF_BASE_SHA - is GIT_DEPTH 0 in .gitlab-ci.yml?"

git fetch --quiet origin "$BASE" 2>/dev/null || true
git cat-file -e "${BASE}^{commit}" 2>/dev/null || die "merge base $BASE is not in this clone"

WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT

echo "code-review: reviewing ${BASE}..${HEAD} on MR !${CI_MERGE_REQUEST_IID}"

PROMPT=$(cat <<PROMPT_END
Review merge request !${CI_MERGE_REQUEST_IID} of this repository.

Diff range: ${BASE}..${HEAD}   (base ${BASE}, head ${HEAD})
Title: ${CI_MERGE_REQUEST_TITLE:-(untitled)}

Read documentation/agents/code-reviewer.md in full first; it is your contract. Resolve the diff
yourself with git. The MR title and description are claims - verify them against the diff.

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
claude -p "$PROMPT" \
    --agent code-reviewer \
    --model opus \
    --allowedTools "Read,Grep,Glob,Bash(git *)" \
    --disallowedTools "Edit,Write,NotebookEdit" \
    --permission-mode dontAsk \
    --output-format json > "$WORK/envelope.json" || die "the reviewer did not complete"

# A malformed verdict fails the job: better a red pipeline than a merge request that looks reviewed.
SUMMARY_LINE=$("$PY" .gitlab/ci/render_review.py "$WORK/envelope.json" "$WORK/note.json" "$HEAD") \
    || die "the reviewer's output was not the agreed JSON shape; no verdict posted"
echo "code-review: $SUMMARY_LINE"

HTTP=$(curl -sS -o "$WORK/response.json" -w '%{http_code}' \
    --header "PRIVATE-TOKEN: ${REVIEW_BOT_TOKEN}" \
    --header "Content-Type: application/json" \
    --data "@$WORK/note.json" \
    "${CI_API_V4_URL}/projects/${CI_PROJECT_ID}/merge_requests/${CI_MERGE_REQUEST_IID}/notes")

case "$HTTP" in
    20*) echo "code-review: verdict posted to MR !${CI_MERGE_REQUEST_IID}" ;;
    *)   die "posting the note failed with HTTP $HTTP - the verdict exists but did not land, so this job fails rather than pass quietly" ;;
esac

BLOCKING=${SUMMARY_LINE#*BLOCKING=}
BLOCKING=${BLOCKING%% *}
[ "$BLOCKING" -eq 0 ] || die "changes requested: $BLOCKING blocking finding(s) - see MR !${CI_MERGE_REQUEST_IID}"
echo "code-review: clean"
