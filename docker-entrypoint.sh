#!/usr/bin/env bash
# Theoriarr container entrypoint.
#
# The image runs the service as the non-root `app` user (uid/gid 1654) by
# default. It only repairs volume ownership when the container is started as
# root (`--user 0`, or `user: root` in compose): it then chowns the mounted
# volumes, swaps to PUID/PGID and execs the service with setpriv, so the
# long-running process is never root. This is only needed for a root-owned or
# host bind-mounted /data.
#
#   PUID / PGID  UID/GID the service runs as when started as root
#              (default: the image's `app` user)
#   TZ           timezone, e.g. Europe/Amsterdam
#
# Starting as an unprivileged user (the default) skips the remap and honours the
# caller's uid/gid.
#
# Privileges are dropped with `setpriv --init-groups` (never `--clear-groups`)
# so the service keeps the supplementary groups it was granted at image build
# time (notably video/render for /dev/dri GPU transcoding).
set -euo pipefail

PUID="${PUID:-1654}"
PGID="${PGID:-1654}"
DATA="${THEORIARR_DATA:-/data}"

# Transcode working folder (large temp files). Defaults to <data>/transcode; a
# dedicated mount can be pointed at with THEORIARR_TRANSCODE_TEMP_FOLDER, and
# must be handed to the service user just like DATA.
TRANSCODE="${THEORIARR_TRANSCODE_TEMP_FOLDER:-$DATA/transcode}"

export HOME="${HOME:-/home/app}"

if [ "$(id -u)" = "0" ]; then
  if [ -n "${TZ:-}" ]; then
    if [ -f "/usr/share/zoneinfo/$TZ" ]; then
      ln -snf "/usr/share/zoneinfo/$TZ" /etc/localtime
      printf '%s\n' "$TZ" > /etc/timezone
      export TZ
    else
      echo "docker-entrypoint: unknown timezone '$TZ', keeping default" >&2
    fi
  fi

  current_uid="$(id -u app)"
  current_gid="$(id -g app)"

  if [ "$PGID" != "$current_gid" ]; then
    groupmod -o -g "$PGID" app
  fi
  if [ "$PUID" != "$current_uid" ]; then
    usermod -o -u "$PUID" app
  fi

  # When we start as root, HOME points at /root; point it at the service user's
  # home before dropping privileges so login shells and tools don't touch /root.
  export HOME=/home/app

  mkdir -p "$DATA" /app/_run "$TRANSCODE"

  # /app is owned by the image's original uid, so only re-map the whole tree
  # when the service uid/gid actually changed; /app/_run is always handed over
  # so pidfiles/logs can be written.
  if [ "$PUID" != "$current_uid" ] || [ "$PGID" != "$current_gid" ]; then
    chown -R app:app /app
  else
    chown app:app /app/_run
  fi

  # $DATA and $TRANSCODE may be pre-existing, root-owned volumes from an earlier
  # image or host bind mounts. Repair them recursively on every start so
  # upgrades (and bind mounts) work. They hold state, not the app image, so the
  # cost tracks the data size rather than the image.
  chown -R app:app /home/app "$DATA" "$TRANSCODE"

  # --init-groups rebuilds the supplementary group list from /etc/group for the
  # target uid, preserving video/render (GPU) membership after the switch.
  exec setpriv --reuid "$PUID" --regid "$PGID" --init-groups "$@"
fi

# Already running as an unprivileged user (explicit `--user`); we cannot
# chown/remap, so honour the caller's uid/gid and just exec.
if [ "$PUID" != "$(id -u)" ] || [ "$PGID" != "$(id -g)" ]; then
  echo "docker-entrypoint: running as non-root (uid $(id -u)); PUID/PGID are ignored." >&2
fi

if [ -n "${TZ:-}" ]; then
  export TZ
fi

mkdir -p "$DATA" /app/_run "$TRANSCODE" 2>/dev/null || true

if ! touch "$DATA/.docker-write-test" 2>/dev/null; then
  echo "docker-entrypoint: $DATA is not writable by uid $(id -u)." >&2
  echo "docker-entrypoint: remove any explicit --user / user: setting so the entrypoint can repair ownership as root." >&2
else
  rm -f "$DATA/.docker-write-test"
fi

exec "$@"
