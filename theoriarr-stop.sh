#!/usr/bin/env bash
# Stop the Theoriarr unified backend started by ./theoriarr.sh.
# Legacy movies/series/gateway/portal pidfiles are cleaned up too, so upgrades
# from the old two-process + gateway layout stop cleanly.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RUN="$ROOT/_run"

PIDS=()
for name in theoriarr movies series gateway portal; do
  pidfile="$RUN/$name.pid"
  [ -f "$pidfile" ] || continue
  pid="$(cat "$pidfile")"
  if kill -0 "$pid" 2>/dev/null; then
    echo "Stopping $name (pid $pid)..."
    kill "$pid" 2>/dev/null || true
    PIDS+=("$name:$pid")
  fi
  rm -f "$pidfile"
done

# Give the service a moment, then force anything still alive.
sleep 3
for entry in ${PIDS[@]+"${PIDS[@]}"}; do
  name="${entry%%:*}"
  pid="${entry#*:}"
  if kill -0 "$pid" 2>/dev/null; then
    echo "Force stopping $name (pid $pid)..."
    kill -9 "$pid" 2>/dev/null || true
  fi
done

echo "Theoriarr stopped."
