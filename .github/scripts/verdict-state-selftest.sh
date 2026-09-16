#!/usr/bin/env bash
# Proves verdict-state.sh's parser accepts what the contract promises and rejects what it does not.
# It drives the REAL parser through `verdict-state.sh --parse`, never a copy: a second parser that
# drifted would let a PR look ruled to one caller and unruled to another, which is the failure this
# whole gate exists to prevent.
set -uo pipefail
cd "$(dirname "$0")/../.."
FAIL=0
t() {
    got=$(printf '%s' "$2" | bash .github/scripts/verdict-state.sh --parse)
    if [ "$got" = "$1" ]; then
        printf '  ok    %-16s %s\n' "$1" "$3"
    else
        printf '  FAIL  expected=%s got=%s  %s\n' "$1" "$got" "$3"; FAIL=1
    fi
}

t approve          '**Verdict: Approve**'                          'canonical approve'
t request-changes  '**Verdict: Request changes**'                  'canonical request changes'
t approve          'Verdict: Approve.'                             'trailing period, no emphasis'
t approve          '**Verdict: Approve** - nice catch on the lock' 'trailing prose is fine'
t request-changes  '**VERDICT: REQUEST CHANGES**'                  'casing is forgiving'
t approve          '   **Verdict:   Approve**'                     'leading and inner whitespace'
t none             'Looks good to me'                              'prose with no verdict'
t none             'I think **Verdict: Approve**'                  'verdict must start the line'
t none             '**Verdict: Approved**'                         'the WORD is strict (Approved)'
t none             '**Verdict: LGTM**'                             'the WORD is strict (LGTM)'
t approve          "$(printf '**Verdict: Approve**\nmore body')"   'verdict on the first line'
t none             "$(printf 'preamble\n**Verdict: Approve**')"    'verdict on the second line'
# GitHub returns review bodies CRLF-terminated, so this is the shape the API actually produces - the
# one the parser must survive in production and the only one nothing else here pins.
t request-changes  "$(printf '**Verdict: Request changes**\r\nmore body')" 'CRLF, as the API returns it'

if [ "$FAIL" -eq 0 ]; then
    echo "ok  the verdict parser still accepts the contract's shapes and rejects the near-misses."
else
    echo "FAILED - the parser and the contract disagree; fix one of them before trusting the gate." >&2
fi
exit "$FAIL"
