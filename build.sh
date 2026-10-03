#!/usr/bin/env bash
# Theoriarr build.
#
# There is ONE backend and ONE UI:
#   * backend: src/Theoriarr.Series (the unified engine — Sonarr-derived tree,
#     now hosting the folded-in Radarr/Movies domain; one listener, one DB)
#   * UI:      src/Theoriarr.Web    (the single unified Theoriarr SPA)
#
# Usage:
#   ./build.sh                 # backend + unified UI
#   SKIP_FRONTEND=1 ./build.sh # backend only
#   CONFIGURATION=Debug ./build.sh # debug backend (enables the diagnostics dump)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export PATH="$PATH:/usr/local/bin:/root/.dotnet"
export DOTNET_ROOT="${DOTNET_ROOT:-/root/.dotnet}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export YARN_CACHE_FOLDER="${YARN_CACHE_FOLDER:-$ROOT/_cache/yarn}"
export NODE_OPTIONS="${NODE_OPTIONS:---max-old-space-size=8192}"

SKIP_FRONTEND="${SKIP_FRONTEND:-0}"
CONFIGURATION="${CONFIGURATION:-Release}"

BACKEND_TREE="Theoriarr.Series"
BACKEND_SLN="Sonarr.sln"

FRONTENDS=(
  "Theoriarr.Web"
)

if [ ! -x /root/.dotnet/dotnet ] && ! command -v dotnet >/dev/null 2>&1; then
  echo "Toolchain missing. Run /root/setup-toolchain.sh first." >&2
  exit 1
fi

echo "=================================================================="
echo "== backend:  $BACKEND_TREE ($BACKEND_SLN, $CONFIGURATION)"
echo "=================================================================="

# Stamp Release builds as official ("Release" instead of "Release-dev") so the
# running app reports production (RuntimeInfo.IsProduction): console/Docker log
# cleansing and outbound redirect handling are enabled in shipped artifacts.
# Debug builds stay non-production.
BUILD_ARGS=()
if [ "$CONFIGURATION" = "Release" ]; then
  BUILD_ARGS+=("-p:OfficialBuild=true")
fi

( cd "$ROOT/src/$BACKEND_TREE" && dotnet build "src/$BACKEND_SLN" -c "$CONFIGURATION" "${BUILD_ARGS[@]}" )

if [ "$SKIP_FRONTEND" != "1" ]; then
  for tree in "${FRONTENDS[@]}"; do
    echo "=================================================================="
    echo "== frontend: $tree (unified UI)"
    echo "=================================================================="
    ( cd "$ROOT/src/$tree" \
        && yarn install --frozen-lockfile --network-timeout 600000 \
        && yarn build --env production )
  done
fi

echo
echo "Theoriarr build complete. Run ./theoriarr.sh to start the unified backend on :6868."
