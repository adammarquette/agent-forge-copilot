#!/usr/bin/env bash
#
# Fail-safe guard against a FALSE GREEN test run, ported from the retired GitLab
# pipeline's test config (git history: fbbf07d). GitHub Actions has no YAML anchors, so the guard lives here
# and each test job calls it rather than triplicating the logic.
#
# Why this exists (GitLab issue #21 — legacy tracker number): `dotnet test` once ran ZERO
# tests and still exited 0. The build stage's artifacts did not carry the NuGet global
# packages cache (it lives outside the checkout), so Microsoft.NET.Test.Sdk.props's
# Condition="Exists(...)" import silently failed to resolve, IsTestProject never got set,
# and MSBuild's VSTest target was silently skipped. A pipeline that reports success while
# executing nothing is worse than a red one.
#
# The GitHub Actions port removes the root cause — every test job runs its own `dotnet
# restore` in its own container, so there is no cross-job artifact to be missing — but the
# guard is kept regardless as a second line of defence against this class of bug recurring
# for any other reason (NFR-REL-2: a meaningful check, not an unconditional pass).
#
# Also writes a run summary to $GITHUB_STEP_SUMMARY. GitLab rendered `reports: junit`
# natively in the MR UI; Actions has no built-in equivalent, and this keeps the pass/fail
# counts visible on the run page without taking a third-party action as a dependency.
#
# Usage: verify-test-results.sh [results-dir]   (default: TestResults)

set -euo pipefail

results_dir="${1:-TestResults}"

if ! ls "$results_dir"/*.junit.xml >/dev/null 2>&1; then
  echo "No test result files were produced in '$results_dir' - dotnet test ran silently and produced nothing (legacy issue #21). Failing rather than reporting a false green."
  exit 1
fi

# Sum one numeric JUnit attribute across every result file.
#
# Done in a single awk rather than `grep | grep | awk` on purpose. Under
# `set -euo pipefail` a grep that matches ZERO times exits 1, which fails the whole
# pipeline and aborts this script — with no output at all, because the failure happens
# inside a command substitution. A JUnit file that simply omits an attribute (some
# loggers drop `errors="0"`) would therefore turn a perfectly good run into a red job
# carrying no diagnostic: the exact misleading-CI-signal failure mode this guard exists
# to prevent. awk has no such behaviour — an attribute that never matches leaves the
# running total at 0, which is the intended reading, and lets a genuinely empty run flow
# into the explicit total_tests check below instead of dying before it.
sum_attr() {
  awk -v attr="$1" '
    {
      while (match($0, attr "=\"[0-9]+\"")) {
        s += substr($0, RSTART + length(attr) + 2, RLENGTH - length(attr) - 3)
        $0 = substr($0, RSTART + RLENGTH)
      }
    }
    END { print s + 0 }
  ' "$results_dir"/*.junit.xml
}

total_tests=$(sum_attr tests)
total_failures=$(sum_attr failures)
total_errors=$(sum_attr errors)
total_skipped=$(sum_attr skipped)

if [ "$total_tests" -eq 0 ]; then
  echo "Test result files exist but report zero tests executed (legacy issue #21). Failing rather than reporting a false green."
  exit 1
fi

echo "Verified: $total_tests test(s) actually executed ($total_failures failure(s), $total_errors error(s), $total_skipped skipped)."

# Job summary - only when running under Actions, so the script stays runnable locally.
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "### ${GITHUB_JOB:-tests} results"
    echo ""
    echo "| Metric | Count |"
    echo "|---|---|"
    echo "| Executed | ${total_tests} |"
    echo "| Failures | ${total_failures} |"
    echo "| Errors | ${total_errors} |"
    echo "| Skipped | ${total_skipped} |"
  } >> "$GITHUB_STEP_SUMMARY"
fi
