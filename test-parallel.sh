#!/usr/bin/env bash
# Theoriarr unit tests, run in PARALLEL.
#
# The test projects are independent assemblies, so instead of waiting for
# each suite in turn (./test.sh) this builds the solution once and then runs all
# suites concurrently with --no-build. On a 16-core box this cuts the wall-clock
# time from ~4-5 min to ~1.5-2 min (the Core suite's ~5,400 tests dominate).
#
# Usage:
#   ./test-parallel.sh              # build, then run all suites in parallel
#   SKIP_BUILD=1 ./test-parallel.sh # skip the initial build (already built)
#
# Exit code is non-zero if any suite fails.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="$PATH:/usr/local/bin:/root/.dotnet"
export DOTNET_ROOT="${DOTNET_ROOT:-/root/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

TREE="$ROOT/src/Theoriarr.Series"
SLN_DIR="$TREE/src"
FILTER="Category!=IntegrationTest&Category!=AutomationTest&Category!=ManualTest"
SKIP_BUILD="${SKIP_BUILD:-0}"

LOGDIR="$(mktemp -d "${TMPDIR:-/tmp}/theoriarr-tests.XXXXXX")"
trap 'rm -rf "$LOGDIR"' EXIT

if [ "$SKIP_BUILD" != "1" ]; then
  echo "=================================================================="
  echo "== building $TREE ($TREE/src/Sonarr.sln)"
  echo "=================================================================="
  ( cd "$TREE" && dotnet build "src/Sonarr.sln" -c Release ) || exit 1
fi

# name -> test project (relative to $TREE)
PROJECTS=(
  "core      NzbDrone.Core.Test/Sonarr.Core.Test.csproj"
  "common    NzbDrone.Common.Test/Sonarr.Common.Test.csproj"
  "api       NzbDrone.Api.Test/Sonarr.Api.Test.csproj"
  "host      NzbDrone.Host.Test/Sonarr.Host.Test.csproj"
  "http      Sonarr.Http.Test/Sonarr.Http.Test.csproj"
  "update    NzbDrone.Update.Test/Sonarr.Update.Test.csproj"
  "libraries NzbDrone.Libraries.Test/Sonarr.Libraries.Test.csproj"
)

pids=()
names=()
for entry in "${PROJECTS[@]}"; do
  read -r name proj <<< "$entry"
  names+=("$name")
  echo "=================================================================="
  echo "== tests: $name / $proj"
  echo "=================================================================="
  (
    cd "$TREE" || exit 1
    dotnet test "src/$proj" -c Release --no-build --no-restore \
      -p:SolutionDir="$SLN_DIR/" \
      --filter "$FILTER" \
      --results-directory "$LOGDIR/$name-results" \
      --logger "console;verbosity=minimal" > "$LOGDIR/$name.log" 2>&1
    echo "$?" > "$LOGDIR/$name.exit"
  ) &
  pids+=($!)
done

for pid in "${pids[@]}"; do wait "$pid"; done

echo
rc=0
for name in "${names[@]}"; do
  exit_code="$(cat "$LOGDIR/$name.exit" 2>/dev/null || echo '?')"
  summary="$(grep -E 'Passed!|Failed!|No test matches' "$LOGDIR/$name.log" | tail -1)"
  printf '[%-9s] exit=%s  %s\n' "$name" "$exit_code" "$summary"
  if [ "$exit_code" != "0" ]; then
    rc=1
    echo "---- $name output tail ----"
    tail -40 "$LOGDIR/$name.log"
    echo "---------------------------"
  fi
done

echo
if [ "$rc" = "0" ]; then
  echo "All Theoriarr test suites passed (parallel)."
else
  echo "One or more Theoriarr test suites FAILED (parallel)."
fi
exit "$rc"
