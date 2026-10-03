#!/usr/bin/env bash
# Theoriarr launcher: runs the single unified backend (src/Theoriarr.Series),
# which hosts BOTH the Radarr (Movies) and Sonarr (Series) API domains on one
# listener (:6868) and serves the single unified UI (src/Theoriarr.Web) from its
# own UI folder. The retired gateway is no longer involved.
#
# The backend exposes both API surfaces on the same origin and selects the
# domain per request from the presented API key, so Prowlarr/Jellyseerr connect
# to this host:6868 with either the Radarr or the Sonarr key.
#
# Usage:
#   ./theoriarr.sh            # start in background, print URL + API keys
#   ./theoriarr.sh --attach   # start, stream the backend log, Ctrl+C stops all
#   ./theoriarr-stop.sh       # stop
set -euo pipefail

ATTACH="${THEORIARR_ATTACH:-0}"
for arg in "$@"; do
  case "$arg" in
    -a | --attach) ATTACH=1 ;;
    *)
      echo "unknown option: $arg (try --attach)" >&2
      exit 2
      ;;
  esac
done

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN="$ROOT/_run"
DATA="${THEORIARR_DATA:-$RUN/data}"
LOGS="$RUN/logs"
PORT="${THEORIARR_PORT:-6868}"
BACKEND_TREE="Theoriarr.Series"

export PATH="$PATH:/usr/local/bin"
if [ -d /root/.dotnet ]; then
  export PATH="$PATH:/root/.dotnet"
  export DOTNET_ROOT="${DOTNET_ROOT:-/root/.dotnet}"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1

mkdir -p "$DATA/Theoriarr" "$LOGS"

find_dll() {
  ls "$ROOT"/src/"$1"/_output/net*/Sonarr.dll 2>/dev/null | head -1
}

BACKEND_DLL="$(find_dll "$BACKEND_TREE")"
if [ -z "$BACKEND_DLL" ]; then
  echo "Built backend not found. Run ./build.sh first." >&2
  exit 1
fi
FW_DIR="$(dirname "$BACKEND_DLL")"

# Stage the single unified UI into the backend's served UI folder
# (<StartUpFolder>/UI). This replaces the retired gateway's static-server job:
# the backend now serves the SPA at the origin root.
UI_SRC="$ROOT/src/Theoriarr.Web/_output/UI"
if [ -d "$UI_SRC" ]; then
  rm -rf "$FW_DIR/UI"
  mkdir -p "$FW_DIR/UI"
  cp -a "$UI_SRC/." "$FW_DIR/UI/"
else
  echo "warning: unified UI not built ($UI_SRC); run ./build.sh" >&2
fi

# Start the detached backend, recording the *real* PID from inside the child.
# `setsid` execs (or forks); either way the child writes its own PID after the
# fork, so the pidfile always matches the live process.
#
# The port is forced to the canonical 6868 and UrlBase to '' through env config
# binding (unified `Theoriarr:Server` section, legacy `Sonarr:Server` fallback),
# so a stale config.xml cannot re-introduce the old :7878/:8989 or /movies,
# /series prefixes.
start_backend() {
  local log="$LOGS/theoriarr.log"
  local pidfile="$RUN/theoriarr.pid"

  # Run with CWD inside the backend tree so the dotnet SDK muxer picks the SDK
  # pinned by that tree's global.json (net10).
  local tree_dir
  tree_dir="$(cd "$(dirname "$BACKEND_DLL")/../.." && pwd)"

  setsid nohup env \
    "Theoriarr__Server__Port=$PORT" \
    "Sonarr__Server__Port=$PORT" \
    "Theoriarr__Server__UrlBase=" \
    "Sonarr__Server__UrlBase=" \
    bash -c 'cd "$1" && echo $$ > "$2"; exec dotnet "$3" --data="$4" --nobrowser' \
    _ "$tree_dir" "$pidfile" "$BACKEND_DLL" "$DATA/Theoriarr" </dev/null > "$log" 2>&1 &
}

is_listening() {
  curl -sS -m 2 -o /dev/null "http://localhost:$1/" 2>/dev/null
}

wait_for_port() {
  local port="$1"
  local tries="${2:-45}"
  while [ "$tries" -gt 0 ]; do
    if is_listening "$port"; then
      return 0
    fi
    sleep 1
    tries=$((tries - 1))
  done
  return 1
}

if is_listening "$PORT"; then
  echo "Theoriarr already listening on :$PORT"
else
  echo "Starting Theoriarr unified backend (:$PORT, UrlBase='')..."
  start_backend
fi

wait_for_port "$PORT" || echo "warning: backend did not respond on :$PORT (see $LOGS/theoriarr.log)"

get_key() {
  local tag="$1"
  local cfg="$DATA/Theoriarr/config.xml"
  [ -f "$cfg" ] || return 0
  python3 -c "import re,sys;m=re.search(r'<'+sys.argv[2]+r'>([^<]+)</'+sys.argv[2]+r'>',open(sys.argv[1]).read());print(m.group(1) if m else '')" "$cfg" "$tag" || true
}

RADARR_KEY="$(get_key MovieApiKey)"
SONARR_KEY="$(get_key ApiKey)"

echo
echo "=================== Theoriarr is running ==================="
echo "Theoriarr UI : http://localhost:$PORT/"
echo "APIs         : /api/v3 (Radarr + Sonarr, selected by key), /api/v5 (Sonarr)"
echo
echo "Jellyseerr -> add Radarr : http://<host>:$PORT   API key: $RADARR_KEY"
echo "Jellyseerr -> add Sonarr : http://<host>:$PORT   API key: $SONARR_KEY"
echo
echo "Data: $DATA/Theoriarr"
echo "Logs: $LOGS/theoriarr.log"
echo "Stop: ./theoriarr-stop.sh"
echo "============================================================"

if [ "$ATTACH" = "1" ]; then
  touch "$LOGS/theoriarr.log"

  echo
  echo "Streaming backend log (press Ctrl+C to stop the unified service)..."
  echo

  tail -n +1 -F "$LOGS/theoriarr.log" &
  TAIL_PID=$!

  shutdown() {
    trap - INT TERM
    kill "$TAIL_PID" 2>/dev/null || true
    wait "$TAIL_PID" 2>/dev/null || true
    echo
    "$ROOT/theoriarr-stop.sh"
    exit 0
  }
  trap shutdown INT TERM

  wait "$TAIL_PID" || true
  shutdown
fi
