#!/usr/bin/env bash
# measure-tok.sh — print the `~tok` price for markdown files, in the form a routing-map row states.
#
#   scripts/measure-tok.sh [repo-root] [file ...]
#
# The unit is bytes / BYTES_PER_TOKEN, rounded to ROUNDING_STEP tokens and written to 0.1K. It is
# deliberately re-derivable in one command: being checkable matters more than being exact.
#
# Paste the printed value into the map row. check-doc-sizes.sh compares against exactly this number and
# measures it the same way, so there is never a reason to compute one by hand.

set -euo pipefail

REPO_ROOT="${1:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
if [ ! -d "$REPO_ROOT" ]; then
  printf '\033[31mNO SUCH ROOT\033[0m  %s\n' "$REPO_ROOT" >&2
  exit 1
fi
shift || true
cd "$REPO_ROOT"

BYTES_PER_TOKEN=4
ROUNDING_STEP=100
# shellcheck source=/dev/null
[ -f documentation/.harness.conf ] && . documentation/.harness.conf

if [ "$#" -gt 0 ]; then
  files="$(printf '%s\n' "$@")"
else
  ls_status=0
  files="$(git ls-files --cached --others --exclude-standard '*.md')" || ls_status=$?
  if [ "$ls_status" -ne 0 ]; then
    printf '\033[31mCANNOT LIST\033[0m  git ls-files exited %s under %s\n' "$ls_status" "$REPO_ROOT" >&2
    exit 1
  fi
  files="$(printf '%s\n' "$files" | sort -u)"
fi

# MEASURED EXACTLY AS check-doc-sizes.sh MEASURES, and the two must not drift apart: an author pastes what
# this prints and the gate compares against what it measures, so any divergence is a row that cannot be
# made green. Both halves of the normalisation are load-bearing, because each one otherwise makes the same
# document price differently on a developer machine than on Linux CI:
#
#   - the trailing CR is dropped, because with core.autocrlf on, a Windows checkout carries one extra byte
#     per line;
#   - LC_ALL=C, because gawk in a UTF-8 locale counts length() in CHARACTERS, and a corpus with em-dashes
#     and curly quotes then measures short of its real byte count.
#
# CR is built with sprintf("%c", 13) rather than written as an escape, so no backslash survives the trip
# through the shell into awk — that round trip is exactly how this line acquired an invisible literal CR
# the first time it was written.
price_of() {
  LC_ALL=C awk -v per="$BYTES_PER_TOKEN" -v step="$ROUNDING_STEP" '
    BEGIN { CR = sprintf("%c", 13) }
    {
      line = $0
      if (substr(line, length(line), 1) == CR) line = substr(line, 1, length(line) - 1)
      total += length(line) + 1
    }
    END { t = total / per; r = int(t / step + 0.5) * step; printf "%d %.1fK\n", total, r / 1000 }
  ' "$1"
}

printf '%-62s %8s %8s\n' "FILE" "BYTES" "~tok"
while IFS= read -r file; do
  [ -n "$file" ] || continue
  [ -f "$file" ] || continue
  read -r bytes price < <(price_of "$file")
  printf '%-62s %8s %8s\n' "$file" "$bytes" "$price"
done <<FILES
$files
FILES
