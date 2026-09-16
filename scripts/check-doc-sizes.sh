#!/usr/bin/env bash
# check-doc-sizes.sh — fail when a `~tok` column stops describing the files it prices.
#
#   scripts/check-doc-sizes.sh [repo-root]
#
# WHY THIS IS CHECKED RATHER THAN REMEMBERED
#
# The routing map prices every document it routes to, so a reader sees what a read costs BEFORE paying for
# it. The drift is structural, not careless: a working-memory file under standing orders to grow is wrong
# again a few entries after anyone corrects it, and no ordinary pull request has a reason to look at the row
# that prices it. Left unchecked this column does not merely go stale — it inverts the advice standing next
# to it, which is the routing map sending every reader the wrong way.
#
# WHAT THE ROW MUST SAY. Not "within N percent" — a proportional band is widest on exactly the documents
# that grow most, so drift accumulates inside it while the ceiling recedes. The row must equal the
# measurement ROUNDED TO THE COLUMN'S OWN GRANULARITY, which is the value this script prints for the author
# to paste. The remaining blind spot is then constant rather than proportional, so it cannot compound.
#
# The comparison is arithmetic, not a string match: `1K` and `1.0K` are the same price and both pass. A cell
# carrying more precision than the column has — `1.05K` — is refused, because the granularity is the rule.
#
# THREE RULES, all enforced below:
#   1. every row in a registered priced table states its file's measured price
#   2. no row's prose makes a size claim in words — the number is the only place a price lives
#   3. no unregistered `~tok` table exists anywhere in the corpus
#
# Every intermediate read below is status-checked rather than streamed, because the defect this family of
# gates keeps producing is a check that passes because it never ran.

set -euo pipefail

REPO_ROOT="${1:-$(git rev-parse --show-toplevel 2>/dev/null || pwd)}"
cd "$REPO_ROOT" || { printf '\033[31mNO SUCH ROOT\033[0m  %s\n' "$REPO_ROOT" >&2; exit 1; }

BYTES_PER_TOKEN=4
ROUNDING_STEP=100
PRICED_TABLES=()
# shellcheck source=/dev/null
[ -f documentation/.harness.conf ] && . documentation/.harness.conf

if [ "${#PRICED_TABLES[@]}" -eq 0 ]; then
  printf '\033[31mNOTHING REGISTERED\033[0m  PRICED_TABLES is empty in documentation/.harness.conf\n' >&2
  printf 'A priced map with no registered table is a map nothing measures. Register it, or drop the\n' >&2
  printf '`~tok` column and this gate together.\n' >&2
  exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
printf '%s\n' "${PRICED_TABLES[@]}" > "$tmp/registered"

# Rule 2's closed vocabulary: superlatives ranking one document against the others, plus the bare cost words
# that claim a price in every use. Bare `-er` comparatives — longer, larger, smaller, faster — are
# DELIBERATELY ABSENT: they fire on ordinary English ("no longer matches the card"), and a gate that reddens
# on correct prose is deleted by the first person it wrongly stops.
CLAIM_WORDS='cheap|cheaper|cheapest|smallest|quickest|fastest|pricey|costly'

failures=0
rows=0

read -r -d '' AWK_ROWS <<'AWKEOF' || true
function trim(s) { gsub(/^[ \t\r]+|[ \t\r]+$/, "", s); return s }
function bare(s) { gsub(/[`*_]/, "", s); return trim(s) }

function normalize(p,   parts, n, out, i, j, r) {
  n = split(p, parts, "/"); j = 0
  for (i = 1; i <= n; i++) {
    if (parts[i] == "." || parts[i] == "") continue
    if (parts[i] == "..") { if (j > 0) j--; continue }
    out[++j] = parts[i]
  }
  r = ""
  for (i = 1; i <= j; i++) r = r (i > 1 ? "/" : "") out[i]
  return r
}

# THE MEASUREMENT IS DONE HERE, IN BYTES, WITH LINE ENDINGS NORMALISED — and both halves of that are
# load-bearing, because each one otherwise makes the same document price differently on a developer
# machine than on Linux CI, turning a green row red in the pipeline with neither number wrong:
#
#   - the trailing CR is dropped, because with core.autocrlf on, a Windows checkout carries one extra
#     byte per line;
#   - the caller exports LC_ALL=C, because gawk in a UTF-8 locale counts length() in CHARACTERS, and a
#     corpus with em-dashes and curly quotes then measures short of its real byte count.
#
# CR is built with sprintf("%c", 13) rather than written as an escape, so no backslash has to survive the
# trip through the shell into awk. Reading the file here rather than shelling out to `wc` also drops a
# subprocess per row and one whole layer of quoting.
function measure(path,   line, total, r) {
  total = 0
  while ((r = (getline line < path)) > 0) {
    if (substr(line, length(line), 1) == CR) line = substr(line, 1, length(line) - 1)
    total += length(line) + 1
  }
  close(path)
  if (r < 0) return -1
  return total
}

# The printed price, and the only form a row may state: bytes / PER, rounded to STEP tokens, written to 0.1K.
function as_k(bytes,   t, r) {
  t = bytes / PER
  r = int(t / STEP + 0.5) * STEP
  return sprintf("%.1fK", r / 1000)
}

function link_target(cell,   t) {
  if (!match(cell, /\]\([^)]+\)/)) return ""
  t = substr(cell, RSTART + 2, RLENGTH - 3)
  sub(/#.*$/, "", t)
  return trim(t)
}

BEGIN { FS = "|"; infence = 0; insection = 0; pricecol = 0; found = 0; CR = sprintf("%c", 13) }

/^[ \t]*(```|~~~)/ { infence = !infence; next }
infence { next }

/^#+[ \t]/ {
  insection = 0
  if (trim($0) == HEADING) { insection = 1; pricecol = 0; found = 1 }
  next
}

!insection { next }
$0 !~ /\|/ { next }

{
  # The header row names the priced column; everything after it is data until the section ends.
  if (pricecol == 0) {
    for (i = 2; i <= NF; i++) if (bare($i) == "~tok") pricecol = i
    next
  }
  if ($0 ~ /^[ \t]*\|[ \t:|-]+\|[ \t]*$/) next
  if (NF < pricecol) next

  label  = $2
  stated = bare($(pricecol))
  if (stated == "") next

  # LOCAL ADAPTATION: scan the row left to right for the first relative link rather than requiring it in
  # column 1. Two of this repo's routing tables lead with the situation ("if you are reviewing a change...")
  # and carry the contract in the next cell; forcing the link first would make the table answer a question
  # nobody asks first. Left-to-right order is what keeps this safe - the contract link precedes the
  # incidental ones (a row may also link the skill or the subagent that delivers it).
  target = ""
  for (c = 2; c <= NF && target == ""; c++) if (c != pricecol) target = link_target($c)
  if (target == "") { printf "NOLINK\t%s\t%s\t%s\n", FILE, trim(label), stated; next }

  resolved = normalize(DIR "/" target)
  bytes = measure(resolved)
  if (bytes < 0) { printf "UNMEASURABLE\t%s\t%s\t%s\n", FILE, resolved, stated; next }

  want = as_k(bytes)

  # One decimal place, because that is what the column can express. `3K` and `3.0K` both render to `3.0K`
  # and both pass; `1.05K` never renders and is refused rather than rounded into agreement.
  if (stated !~ /^[0-9]+(\.[0-9])?K$/) {
    printf "PRECISION\t%s\t%s\t%s\t%s\n", FILE, resolved, stated, want; next
  }
  sn = stated; sub(/K$/, "", sn)
  if (sprintf("%.1fK", sn + 0) != want) {
    printf "STALE\t%s\t%s\t%s\t%s\n", FILE, resolved, stated, want; next
  }

  lc = " " tolower($0) " "
  if (match(lc, "[^a-z](" CLAIMS ")[^a-z]")) {
    printf "SIZECLAIM\t%s\t%s\t%s\n", FILE, resolved, substr(lc, RSTART + 1, RLENGTH - 2); next
  }

  printf "OK\t%s\t%s\t%s\n", FILE, resolved, stated
}

END { if (!found) printf "NOHEADING\t%s\t%s\n", FILE, HEADING }
AWKEOF

while IFS= read -r entry; do
  [ -n "$entry" ] || continue
  file="${entry%%|*}"
  heading="${entry#*|}"
  if [ ! -f "$file" ]; then
    printf '\033[31mNO SUCH FILE\033[0m  %s (registered in PRICED_TABLES)\n' "$file" >&2
    failures=$((failures + 1))
    continue
  fi

  awk_status=0
  LC_ALL=C awk -v FILE="$file" -v DIR="$(dirname "$file")" -v HEADING="$heading" \
      -v PER="$BYTES_PER_TOKEN" -v STEP="$ROUNDING_STEP" -v CLAIMS="$CLAIM_WORDS" \
      "$AWK_ROWS" "$file" > "$tmp/rows" || awk_status=$?
  if [ "$awk_status" -ne 0 ]; then
    printf '\033[31mCANNOT PARSE\033[0m  awk exited %s on %s — its rows were NOT checked.\n' \
      "$awk_status" "$file" >&2
    exit 1
  fi

  while IFS=$'\t' read -r kind a b c d; do
    case "$kind" in
      OK) rows=$((rows + 1)) ;;
      STALE)
        printf '\033[31mSTALE PRICE\033[0m  %s  row for %s says %s, measures %s\n' "$a" "$b" "$c" "$d" >&2
        failures=$((failures + 1)) ;;
      PRECISION)
        printf '\033[31mPRECISION\033[0m  %s  row for %s says %s; the column is written to 0.1K — use %s\n' \
          "$a" "$b" "$c" "$d" >&2
        failures=$((failures + 1)) ;;
      SIZECLAIM)
        printf '\033[31mSIZE CLAIM\033[0m  %s  row for %s says "%s" in prose. The ~tok cell is the only\n' \
          "$a" "$b" "$c" >&2
        printf '            place a price lives, and only the number ever gets corrected.\n' >&2
        failures=$((failures + 1)) ;;
      NOLINK)
        printf '\033[31mNO LINK\033[0m  %s  priced row "%s" (%s) has no relative link to measure\n' \
          "$a" "$b" "$c" >&2
        failures=$((failures + 1)) ;;
      UNMEASURABLE)
        printf '\033[31mUNMEASURABLE\033[0m  %s  row points at %s, which could not be read\n' "$a" "$b" >&2
        failures=$((failures + 1)) ;;
      NOHEADING)
        printf '\033[31mNO SUCH HEADING\033[0m  %s has no heading "%s" (registered in PRICED_TABLES)\n' \
          "$a" "$b" >&2
        printf '                 A registered table nothing matches measures nothing, silently.\n' >&2
        failures=$((failures + 1)) ;;
    esac
  done < "$tmp/rows"
done < "$tmp/registered"

# RULE 3 — no unregistered priced table anywhere in the corpus. A new price list joins this gate
# deliberately, or not at all. Fenced code is skipped, so a document explaining the column can print an
# example table without being accused of adding one.
read -r -d '' AWK_SWEEP <<'AWKEOF' || true
function trim(s) { gsub(/^[ \t\r]+|[ \t\r]+$/, "", s); return s }
function bare(s) { gsub(/[`*_]/, "", s); return trim(s) }
BEGIN { FS = "|" }
FNR == 1 { infence = 0; heading = "" }
/^[ \t]*(```|~~~)/ { infence = !infence; next }
infence { next }
/^#+[ \t]/ { heading = trim($0); next }
$0 !~ /\|/ { next }
{
  for (i = 2; i <= NF; i++) if (bare($i) == "~tok") {
    key = FILENAME "|" heading
    if (!(key in seen)) { seen[key] = 1; print key }
  }
}
AWKEOF

ls_status=0
allmd="$(git ls-files --cached --others --exclude-standard '*.md')" || ls_status=$?
if [ "$ls_status" -ne 0 ] || [ -z "$allmd" ]; then
  printf '\033[31mCANNOT LIST\033[0m  no markdown files listed under %s — the sweep did not run, so this\n' \
    "$REPO_ROOT" >&2
  printf 'cannot report the corpus clean.\n' >&2
  exit 1
fi
printf '%s\n' "$allmd" | sort -u > "$tmp/allmd"
mapfile -t mdfiles < "$tmp/allmd"
awk "$AWK_SWEEP" "${mdfiles[@]}" | sort -u > "$tmp/found"

while IFS= read -r key; do
  [ -n "$key" ] || continue
  if ! grep -Fqx "$key" "$tmp/registered"; then
    printf '\033[31mUNREGISTERED TABLE\033[0m  %s\n' "$key" >&2
    printf '                    A `~tok` column nothing measures drifts silently. Add it to\n' >&2
    printf '                    PRICED_TABLES in documentation/.harness.conf, or drop the column.\n' >&2
    failures=$((failures + 1))
  fi
done < "$tmp/found"

if [ "$failures" -gt 0 ]; then
  printf '\n\033[31m%d priced-row problem(s).\033[0m Paste the measured value; it is printed above.\n' \
    "$failures" >&2
  exit 1
fi

printf '\033[32mok\033[0m  %d priced row(s) state what their files measure.\n' "$rows"
