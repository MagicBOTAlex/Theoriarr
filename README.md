<p align="center">
  <img src="Logo/theoriarr.svg" alt="Theoriarr" width="128" />
</p>

<h1 align="center">Theoriarr</h1>

<p align="center"><strong>Movies and TV, together in one place.</strong></p>

Theoriarr is a unified fork/merge of [Sonarr](https://github.com/Sonarr/Sonarr) and
[Radarr](https://github.com/Radarr/Radarr): one backend, one UI and one port that manages movies,
TV series and anime while preserving the Sonarr and Radarr HTTP APIs. It is a **drop-in
replacement** for both.

## What Theoriarr adds

Features upstream Sonarr and Radarr do not have:

- **One app for movies, TV and anime** — a single backend, UI and port.
- **Both APIs on one listener** — the Radarr and Sonarr APIs (`/api/v3`, plus Sonarr's `/api/v5`) are served together and selected per request by the API key, so Prowlarr / Jellyseerr / Overseerr is still fully supported
- **Built-in media transcoding (compression)** — re-encode library files to H.264, H.265/HEVC or AV1 (or remux the container) with NVIDIA (NVENC)/VA-API GPU or software encoders, per-device controls, reusable profiles, job scheduling and a review step before any original is replaced (library-to-library only; no on-import compression yet, and no AMD gpu support. (I don't have an AMD gpu)).
- **Unified Wanted and Activity** — movie and TV queues and history in one place.
- **Self-hosted metadata** — metadata is served by a [Providarr](https://github.com/MagicBOTAlex/Providarr) instance (TMDb + TheTVDB) instead of Sonarr/Radarr-operated servers. (Servarr-brokered integrations are disabled for now)

# Disclaimer, absolute AI LLM
This project was a trial of LLM agentic programming capabilities. \
I spent 300CNY on deepseek-4.1-flash and 10 days, supervising it (cucking). \
I let it run, while I did Uni assignments. \
I have not touched a single line of code.

I placed the model in a docker container Ubuntu sandbox and let it run with all permissions.

I am not responsible if anything breaks, or if anything happens when using this software. \
But if you find problems, please submit them as issues or make a pull request. \
I have no idea what is going on with the code tho, so expect me letting the AI fix any problems.

Am I proud? No \
Does it work? Yeah (At least for me)

You have been warned!

I actively use this and I find it better than Sonarr and Radarr. \
But, this would not have been possible without Sonarr's and Radarr's open source. \
Any donations, please support them and not me. \
This project is really just their code, but regurgitated.
Expect tons of bugs!

Everything under here is AI written.



## Quick start

Building requires the .NET SDK 10 and Node 24 (Node is only needed to build the SPA):

```bash
./build.sh          # builds the unified backend + UI into _output/
./theoriarr.sh      # starts the listener on http://localhost:6868
```

### Docker

```bash
docker compose up -d --build
```

The entrypoint starts as root only to repair the ownership of the mounted volumes, then drops to
the non-root `app` user (uid/gid 1654, override with `PUID`/`PGID`) for the service itself. It
listens on `:6868` and keeps config, databases, logs and media metadata in the `theoriarr-data`
volume (transcode scratch lives in `theoriarr-transcode`). See
[`docker-compose.yml`](docker-compose.yml) for the optional NVIDIA / VA-API GPU blocks;
`CONFIGURATION=Debug` enables the debug-only diagnostics endpoint.

### Nix

The [flake](flake.nix) provides the toolchain, the built package and a NixOS module:

```bash
nix develop          # dev shell (.NET 10, Node 24, yarn, ffmpeg)
nix run .#build      # build the backend + UI
nix run              # start the backend (after building)
```

```nix
services.theoriarr = {
  enable = true;
  openFirewall = true;
};
```

The module runs the unified backend from `/var/lib/theoriarr` as the `theoriarr` user.

## Credits

This project contains code from [Sonarr](https://github.com/Sonarr/Sonarr) (Team Sonarr)
and [Radarr](https://github.com/Radarr/Radarr) (Team Radarr), both licensed under GPL-3.0.
All credit for the underlying applications belongs to their respective teams and
contributors.

## Upstream service dependencies

Theoriarr runs **fully self-hosted**, without depending on Sonarr/Radarr-operated servers:

- **Metadata** is served by a [Providarr](https://github.com/MagicBOTAlex/Providarr) instance. Set it under **Settings > Metadata Source** (restart required) or via `THEORIARR__METADATA__PROVIDARRBASEURL` (env wins), or leave it unset to use the shared public `https://providarr.deprived.dev`; the `skyhook.sonarr.tv` / `api.radarr.video` / `services.sonarr.tv` URLs are no longer compiled in. Requests to the default host retry with progressive backoff + jitter when it is offline, rate-limited or returning 5xx. **Hosting your own instance is recommended** — the shared one caches aggressively and rate-limits per client (see the [Providarr README](https://github.com/MagicBOTAlex/Providarr#readme)), and Theoriarr surfaces its cache policy as a health warning.
- Scene mapping uses the independent [TheXEM](https://thexem.info) service; daily-series detection comes from TheTVDB air days.

**Intentionally disabled.** Trakt, Simkl and AniList import lists, MyAnimeList import lists, and Trakt notifications authenticate through Servarr-operated servers (`auth.servarr.com` / `services.sonarr.tv`) and are deliberately disabled so Theoriarr never contacts them. They can be re-enabled once a self-hosted OAuth broker is available (set `ServarrAuthDependencies.Enabled = true` and repoint the broker URLs); **request one and it will be implemented.**

## Attribution

This product uses the TMDB API but is not endorsed or certified by TMDB. Metadata may also be
served via a self-hosted [Providarr](https://github.com/MagicBOTAlex/Providarr) instance, which
uses the TMDb and TheTVDB APIs; TheTVDB terms of use apply.

## License

Theoriarr is licensed under the **GNU General Public License v3.0**. The full license text
is in [LICENSE](LICENSE). Copyright notices and license files from Sonarr and Radarr are
preserved in their original locations.

## Modification notice (GPLv3 §5(a))

This is a modified version of Sonarr and Radarr. Modifications made 2026 by the Theoriarr
project.

The only user-visible/legal change is that the combined product is called **Theoriarr**;
upstream files and their notices are preserved as-is. Project/assembly names remain `Sonarr.*` and
runtime process/update constants stay aligned to the real Theoriarr artifacts (no rename), keeping
upstream notices and API compatibility intact. This project is not affiliated with or endorsed by
Team Sonarr or Team Radarr.
