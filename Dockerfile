# syntax=docker/dockerfile:1

# Theoriarr image: builds the single unified backend (src/Theoriarr.Series,
# .NET 10) plus the single unified SPA (src/Theoriarr.Web) and runs them on one
# listener (:6868). No gateway, no reverse proxy, no second process.
#
#   build:   docker build -t theoriarr .
#   run:     docker run --rm -p 6868:6868 -v theoriarr-data:/data theoriarr
#   compose: docker compose up --build

# Node is only needed to build the SPA; it is not part of the runtime image.
# Pinned to the Node release declared in src/Theoriarr.Web/package.json.
FROM node:24.19.0-bookworm-slim AS node

# ---------------------------------------------------------------------------
# Build stage: .NET SDK 10.0.401 (pinned by src/Theoriarr.Series/global.json)
# plus Node 24 / Yarn 1.22.22 for the React frontend.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.401 AS build
SHELL ["/bin/bash", "-o", "pipefail", "-c"]

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    DOTNET_ROOT=/usr/share/dotnet \
    NODE_OPTIONS=--max-old-space-size=8192 \
    YARN_CACHE_FOLDER=/usr/local/share/.cache/yarn \
    COREPACK_ENABLE_DOWNLOAD_PROMPT=0

# Node + Corepack come from the pinned Node image; Corepack provisions the Yarn
# version declared by package.json's "packageManager" field (no hard-coded
# /opt/yarn-vX path).
COPY --from=node /usr/local /usr/local

# The Node image's yarn/yarnpkg/pnpm shims point at /opt/yarn-vX, which is not
# copied (only /usr/local is), so they are dangling here and make `corepack
# enable` fail on realpath. Remove them so Corepack installs its own shims.
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates git \
 && rm -rf /var/lib/apt/lists/* \
 && rm -f /usr/local/bin/yarn /usr/local/bin/yarnpkg /usr/local/bin/pnpm \
 && corepack enable \
 && dotnet --list-sdks \
 && node --version

WORKDIR /src

# Prime frontend dependencies in their own layer so source edits don't refetch.
COPY src/Theoriarr.Web/package.json src/Theoriarr.Web/yarn.lock src/Theoriarr.Web/.yarnrc src/Theoriarr.Web/
RUN --mount=type=cache,target=/usr/local/share/.cache/yarn,sharing=locked \
    cd src/Theoriarr.Web \
    && yarn --version \
    && yarn install --frozen-lockfile --network-timeout 600000

# Full source (node_modules/_output excluded via .dockerignore), then the
# project's own build: unified backend + unified UI.
COPY . .

# Backend build configuration: Release (default) or Debug. A Debug build enables
# the debug-only diagnostics endpoint (GET /api/v3/system/diagnostics). Enable it
# by building with `CONFIGURATION=Debug docker compose up -d --build` (compose
# forwards CONFIGURATION here) or `docker build --build-arg CONFIGURATION=Debug`.
ARG CONFIGURATION=Release

RUN --mount=type=cache,target=/root/.nuget/packages,sharing=locked \
    --mount=type=cache,target=/usr/local/share/.cache/yarn,sharing=locked \
    CONFIGURATION=$CONFIGURATION ./build.sh

# ---------------------------------------------------------------------------
# Runtime stage: ASP.NET Core runtime 10, python3 (theoriarr.sh reads the API
# keys out of config.xml) and curl (healthcheck).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12 AS runtime
SHELL ["/bin/bash", "-o", "pipefail", "-c"]

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    THEORIARR_DATA=/data \
    THEORIARR_PORT=6868 \
    PATH=/usr/local/bin:/usr/bin:/bin:/usr/sbin:/sbin

# ffmpeg powers media compression (NVENC / VA-API / software; resolved from PATH).
# vainfo is optional but makes VA-API capability diagnosis easier. passwd
# provides usermod/groupmod used by the entrypoint when it starts as root.
#
# The `app` user is added to the video/render groups so hardware transcoding can
# access /dev/dri. The entrypoint uses `setpriv --init-groups` (not
# `--clear-groups`) so these supplementary groups survive the privilege drop.
RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      curl ca-certificates python3-minimal tzdata util-linux ffmpeg vainfo passwd \
 && rm -rf /var/lib/apt/lists/* \
 && (getent group video >/dev/null || groupadd -r video) \
 && (getent group render >/dev/null || groupadd -r render) \
 && usermod -aG video,render app \
 && dotnet --list-runtimes \
 && ffmpeg -version | head -1

WORKDIR /app

# Keep the repo layout so theoriarr.sh's find_dll resolves exactly as it does
# from a normal checkout.
COPY --from=build /src/src/Theoriarr.Series/_output ./src/Theoriarr.Series/_output
COPY --from=build /src/src/Theoriarr.Web/_output/UI ./src/Theoriarr.Web/_output/UI
COPY theoriarr.sh theoriarr-stop.sh ./
COPY Logo ./Logo
# License/attribution notices must accompany GPL binary redistribution.
COPY LICENSE NOTICE ./
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh

# /data backs the named volume; /transcode is the default transcode working
# folder (mount a volume there to persist it). The entrypoint starts as root so
# it can repair the ownership of a pre-existing or bind-mounted volume, then
# drops privileges to PUID/PGID (default 1654) and runs the service as the
# non-root `app` user.
RUN mkdir -p /data /transcode \
 && chown -R app:app /app /data /transcode \
 && chmod +x ./theoriarr.sh ./theoriarr-stop.sh /usr/local/bin/docker-entrypoint.sh

VOLUME ["/data"]

EXPOSE 6868

HEALTHCHECK --interval=30s --timeout=5s --start-period=90s --retries=5 \
  CMD curl -fsS "http://localhost:${THEORIARR_PORT:-6868}/ping" || exit 1

# No USER directive: the entrypoint must start as root to chown a mounted
# volume, then it drops to PUID/PGID (the non-root `app` user) with setpriv
# before exec'ing the service. Starting with `--user <uid>` still works and
# skips the remap.
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
CMD ["./theoriarr.sh", "--attach"]
