#!/usr/bin/env bash
# Theoriarr unit tests: runs the unified backend's unit (non-integration) test
# projects.
#
# Usage:
#   ./test.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="$PATH:/usr/local/bin:/root/.dotnet"
export DOTNET_ROOT="${DOTNET_ROOT:-/root/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1

FILTER="Category!=IntegrationTest&Category!=AutomationTest&Category!=ManualTest"

run_suite() {
  local tree="$1"; shift
  local sln_dir="$ROOT/src/$tree/src"
  for proj in "$@"; do
    echo "=================================================================="
    echo "== tests: $tree / $proj"
    echo "=================================================================="
    ( cd "$ROOT/src/$tree" && dotnet test "src/$proj" -c Release \
        -p:SolutionDir="$sln_dir/" \
        --filter "$FILTER" \
        --logger "console;verbosity=minimal" )
  done
}

run_suite Theoriarr.Series \
  NzbDrone.Core.Test/Sonarr.Core.Test.csproj \
  NzbDrone.Common.Test/Sonarr.Common.Test.csproj \
  NzbDrone.Api.Test/Sonarr.Api.Test.csproj \
  NzbDrone.Host.Test/Sonarr.Host.Test.csproj \
  Sonarr.Http.Test/Sonarr.Http.Test.csproj \
  NzbDrone.Update.Test/Sonarr.Update.Test.csproj \
  NzbDrone.Libraries.Test/Sonarr.Libraries.Test.csproj

echo
echo "Theoriarr test suites complete."
