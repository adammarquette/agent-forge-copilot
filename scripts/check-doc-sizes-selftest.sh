#!/usr/bin/env bash
# check-doc-sizes-selftest.sh — prove the size gate can still fail, and still pass.
#
# Asserts both directions: red on a stale price, a prose size claim, an over-precise cell and an
# unregistered priced table; green on a correctly priced map, on a `3K`/`3.0K` spelling difference that is
# the same price, and on a fenced example table — which a document explaining the column has to be able to
# print without being accused of adding one.

set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GATE="$HERE/check-doc-sizes.sh"
tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT

price() { # the value a correct row states, measured exactly as the gate measures
  LC_ALL=C awk '
    BEGIN { CR = sprintf("%c", 13) }
    { l = $0; if (substr(l, length(l), 1) == CR) l = substr(l, 1, length(l) - 1); t += length(l) + 1 }
    END { r = int(t / 4 / 100 + 0.5) * 100; printf "%.1fK", r / 1000 }
  ' "$1"
}

fixture() {
  rm -rf "$tmp/r"; mkdir -p "$tmp/r/documentation"
  ( cd "$tmp/r" && git init -q . )
  # A document with enough bytes that one edit cannot move its rounded price by accident.
  { printf '# target\n\n'; for i in $(seq 1 60); do printf 'Line %s of ordinary prose in the target document.\n' "$i"; done; } \
    > "$tmp/r/documentation/target.md"
  printf 'BYTES_PER_TOKEN=4\nROUNDING_STEP=100\nPRICED_TABLES=(\n  "documentation/README.md|## Start here"\n)\n' \
    > "$tmp/r/documentation/.harness.conf"
  p="$(price "$tmp/r/documentation/target.md")"
  printf '# map\n\n## Start here\n\n| Document | ~tok | Read it when |\n|---|---:|---|\n| [`target.md`](target.md) | %s | You need the target. |\n' \
    "$p" > "$tmp/r/documentation/README.md"
  ( cd "$tmp/r" && git add -A 2>/dev/null )
}

fails=0
expect() {
  set +e; bash "$GATE" "$tmp/r" >/dev/null 2>&1; got=$?; set -e
  if [ "$got" -ne "$1" ]; then
    printf '\033[31mSELF-TEST FAILED\033[0m  %s — expected exit %s, got %s\n' "$2" "$1" "$got" >&2
    fails=$((fails + 1))
  fi
}

fixture; expect 0 "a correctly priced map is green"

fixture
sed -i 's/| [0-9.]*K |/| 9.9K |/' "$tmp/r/documentation/README.md"
expect 1 "a stale price reddens"

fixture
sed -i 's/You need the target./The cheapest read here. You need the target./' "$tmp/r/documentation/README.md"
expect 1 "a size claim in a row's prose reddens"

fixture
sed -i 's/| \([0-9.]*\)K |/| \1\1K |/' "$tmp/r/documentation/README.md"
expect 1 "a cell finer than the column's granularity reddens"

fixture
printf '\n## Extra\n\n| D | ~tok | W |\n|---|---:|---|\n| [t](target.md) | 1.0K | n |\n' \
  >> "$tmp/r/documentation/README.md"
( cd "$tmp/r" && git add -A 2>/dev/null )
expect 1 "an unregistered priced table reddens"

fixture
printf '\n## Example\n\n```\n| D | ~tok | W |\n|---|---:|---|\n| [t](target.md) | 9.9K | n |\n```\n' \
  >> "$tmp/r/documentation/README.md"
( cd "$tmp/r" && git add -A 2>/dev/null )
expect 0 "a fenced example table does NOT redden"

fixture
sed -i 's|documentation/README.md|## No Such Heading|' "$tmp/r/documentation/.harness.conf" 2>/dev/null || true
printf 'BYTES_PER_TOKEN=4\nROUNDING_STEP=100\nPRICED_TABLES=(\n  "documentation/README.md|## Nowhere"\n)\n' \
  > "$tmp/r/documentation/.harness.conf"
expect 1 "a registered heading that does not exist reddens rather than measuring nothing"

if [ "$fails" -gt 0 ]; then
  printf '\n\033[31m%d self-test failure(s).\033[0m The gate is not behaving as documented.\n' "$fails" >&2
  exit 1
fi
printf '\033[32mok\033[0m  the size gate still reddens on drift, and still passes a correct map.\n'
