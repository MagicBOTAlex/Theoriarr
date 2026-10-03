# Theoriarr — Deferred by Choice & Accepted Limitations

> Deliberately-not-done items that are fully understood and cheap to act on **if a Theoriarr user
> asks for it**. Two kinds live here:
>
> - **Deferred features** (`DF#`) — not implemented; the exact fix is recorded so a future agent can
>   add it without redoing the analysis.
> - **Accepted limitations / divergences** — deliberate tradeoffs that stand on their own (no user
>   request implied), documented so they are not mistaken for bugs.
>
> This is the only remaining project register. The earlier engineering docs (`PROGRESS.md`,
> `PROBLEMS.md`, `RESOLVED.md`, `unified-backend-architecture.md`) were removed on 2026-10-02 to
> keep the public repo lean; development history lives in `git log`.
>
> **Implementing an item?** Do it, remove its record from this file, and describe the change in the
> commit message. **Finding a new one?** Add it here with `file:line` evidence and the exact fix, so
> a future agent can implement it without redoing the analysis.

Severity: **Blocker** > **High** > **Medium** > **Low**.

---

## Deferred features

### DF1 — Low — DEFERRED — no scheduled movie / collection metadata refresh

**What.** `TaskManager.Handle(ApplicationStartedEvent)` registers the series refresh but none of
the movie refreshes (it is Sonarr's task list verbatim):

```
new ScheduledTask { Interval = 12 * 60, TypeName = typeof(RefreshSeriesCommand).FullName }
```
`src/Theoriarr.Series/src/NzbDrone.Core/Jobs/TaskManager.cs:100-104`

Upstream Radarr additionally schedules `RefreshMovieCommand` and `RefreshCollectionsCommand` at
12h. Startup deletes any scheduled task that is absent from that default list
(`TaskManager.cs:141-148`), so a movie/collection refresh schedule can never survive — even though
migration `277` seeds a `ScheduledTasks` row for `RefreshMovieCommand`.

**Effect.** `GET /movie` metadata (overview, artwork, ratings, release dates, status,
translations, credits) and collection metadata are only re-fetched when a movie/collection is
added (`Movies/MovieAddedHandler.cs:21`, `Movies/Collections/MovieCollectionAddedHandler.cs:20`)
or a manual Refresh/command is issued. Older installs can show stale posters, dates, status and
availability, and the descriptions the Library search indexes can go stale.

**Why deferred.** The churn for a settled movie is low, and the work is near-nil if added — the
movie-side command is explicitly built to be schedulable:
`RefreshMovieCommand.UpdateScheduledTask => MovieIds.Empty()`
(`Movies/Commands/RefreshMovieCommand.cs:25`), the no-ids path refreshes only movies the provider
reports changed plus recent/upcoming titles (`Movies/RefreshMovieService.cs:256-297`,
`GetChangedMovies(LastStartTime)` + `Movies/ShouldRefreshMovie.cs:30-57`, which skips anything
older than ~180 days), and `RefreshCollectionsCommand` mirrors this
(`Movies/Commands/RefreshCollectionsCommand.cs`, `Movies/RefreshCollectionService.cs`).

**Fix if requested.** Append to the `defaultTasks` list in `Jobs/TaskManager.cs` (both commands
live in `NzbDrone.Core.Movies.Commands`, so add that using):

```csharp
new ScheduledTask
{
    Interval = 12 * 60,
    TypeName = typeof(RefreshMovieCommand).FullName
},

new ScheduledTask
{
    Interval = 12 * 60,
    TypeName = typeof(RefreshCollectionsCommand).FullName
}
```

**Verify.** Build clean; start the backend; both tasks appear in `GET /api/v3/system/task` (movie
key) / the Tasks UI; a manual run refreshes only changed/recent movies.

### DF2 — Low — DEFERRED — no scheduled / automatic media compression

**What.** Media compression is manual-only: jobs are created through
`POST /api/v3/media-compression/jobs` (or the SPA) and processed by the
`TranscodeService` pump. There is no `ScheduledTask` entry for it and no
"compress when a file is imported / when free space is low / on a schedule" rule.

**Why deferred.** The plan (phase 2+ note) deliberately ships manual triggers first so the
destructive finalize step is always user-reviewed; automatic compression compounds quality risk
and free-space assumptions and needs an opt-in policy model.

**Fix if requested.** Add a `TranscodeScheduledCommand : Command` that queues jobs for a filter
(e.g. files above N GB / older than X / matching a tag), register it in
`Jobs/TaskManager.cs`'s `defaultTasks` (evidence: the `RefreshSeriesCommand` entry
`src/Theoriarr.Series/src/NzbDrone.Core/Jobs/TaskManager.cs:100-104`), and expose the schedule
in the compression settings resource.

**Verify.** Task appears in `GET /api/v3/system/task`; a manual run queues the expected files and
the pump processes them.

---

### DF3 — Low — DEFERRED — resolve/transfer runs synchronously in the HTTP request

**What.** `POST /media-compression/jobs/{id}/resolve` and `.../bulk-resolve` copy the finished output
into the library inside the request (`MediaCompressionController` → `TranscodeService.Resolve` /
`ResolveJobs`; the multi-GB `TransferOutput` runs before the response). A slow/cross-volume copy can
hold the request for minutes and a client/proxy timeout aborts the HTTP call (the copy itself keeps
running, and the 2026-09-30 wave made it cancellable, so the damage is bounded).

**Why deferred.** Making it asynchronous is a contract change (the response can no longer return the
resolved job) and touches the SPA's bulk-resolve flow; the current synchronous copy is well-tested
and cancellable.

**Fix if requested.** `ClaimTransfer` (fast) → return `202 Accepted`, run `FinishTransfer` on the
pump/`Task.Run`, and have the SPA poll the job's `Transferring`→`Completed` status (it already
polls). The response can reuse the existing per-id bulk success/failure payload.

### DF5 — Low — DEFERRED — orphan ffmpeg survives an unclean restart

**What.** On startup `RecoverInterruptedJobs` marks `Running` rows failed and deletes their working
folder, but the ffmpeg child process is not killed and no PID is persisted, so a crash leaves the
encoder reparented and running.

**Why deferred.** Reaping needs a persisted process id (a new `TranscodeJob` column + migration) and
a shutdown hook; an unclean restart is rare and the orphan is CPU/GPU-hungry but not destructive.

**Fix if requested.** Add a nullable `ProcessId` column (migration after `322`), persist it when
`FFmpegTranscodeRunner` starts, and kill a surviving PID (via `IProcessProvider`) in recovery and on
application shutdown.

---

### DF6 — Low — DEFERRED — backend logo is still upstream Sonarr's

**What.** `src/Theoriarr.Series/Logo/*` (including `Sonarr.ico`) is byte-identical to upstream
Sonarr, so the backend ships Sonarr's icon set instead of a Theoriarr one.

**Why deferred.** Cosmetic only; it needs new artwork (16→1024 PNG + `.ico` + `.icns`) before it can
be applied, and the existing icons are valid and correctly licensed (GPL-3.0).

**Fix if requested.** Replace the icon set with Theoriarr artwork, rename `Sonarr.ico`, and update
the installer/SPA references if the filenames change.

**Verify.** Build clean; the app icon, installer and web favicon show the new artwork.

---

## Accepted limitations / divergences (by design)

> Moved out of `PROBLEMS.md` on 2026-09-24 (IDs kept for cross-references). These are deliberate;
> do not "fix" them without a user request, and do not treat them as bugs.

### B1 — Low — accepted divergence — Movie `titleSlug` for library rows is the internal id, not the TMDB id
`src/Theoriarr.Series/src/Sonarr.Api.V3/Movies/MovieResource.cs:157` returns the internal row id
for library rows (`Id > 0`); upstream Radarr returns the TMDB id
(`Radarr/src/Radarr.Api.V3/Movies/MovieResource.cs:151`). Jellyseerr persists `titleSlug` as the
"open in Radarr" slug (`seerr/server/subscriber/MediaRequestSubscriber.ts:398-399`,
`seerr/server/lib/scanners/radarr/index.ts:154`) and builds `${externalUrl}/movie/${titleSlug}`
(`seerr/server/entity/Media.ts:319-337`). **This is deliberate**: the unified SPA resolves
`/movie/:id` against the internal id, and ids and TMDB ids are both numeric and can collide, so
the two cannot share one route. The default Jellyseerr flow (externalUrl = Theoriarr) works; only
pointing `externalUrl` at a *genuine* Radarr UI would 404. Changing it would require reworking
the SPA's movie routing and every internal movie link.

### U2 — Low — accepted limitation — unified Wanted/Activity fetch the full set client-side
`Library/Wanted/useMediaWanted.ts` and `Library/Activity/useMediaActivity.ts` merge the series and
movie endpoints in the SPA and request up to 100000 rows per domain (Wanted also requests both
`monitored` flags, so four calls per tab) so the combined tables can filter/search/page
client-side. There is no client-side cap, so a very large wanted/history/queue/blocklist set
means a large payload; the previous series pages used server-side paging, custom filter builders
and sort options. A proper fix is a backend unified endpoint with real server-side
paging/filtering. Accepted for now; revisit if libraries grow large.

### U12 — Low — accepted limitation — Library search rebuilds an in-memory Lucene index
`Library/LibrarySearchService.cs` backs the search with an embedded **Lucene.NET** index
(`RAMDirectory`) built from `ISeriesService.GetAllSeries()` + `IMovieService.GetAllMovies()`,
exposed as `GET /api/v3/library/search?term=…` by
`Sonarr.Api.V3/Library/LibrarySearchController.cs` (dual-tagged so either API key can call it).
Each field is stored as one case/diacritic-folded token and queried with boosted
`WildcardQuery("*term*")` + `PrefixQuery`, giving true substring semantics; the SPA debounces
250 ms (`useLibrarySearch.ts`). This is the server-side replacement for the previous browser-side
Fuse.js search and removes the fuzzy false positives ("smokin"/"smoking" matching unrelated
shows). Limitation: the whole index is rebuilt (materialising both full sets) on the first query
after a 30 s lifetime, so it is O(library size) periodically and results can be up to ~30 s stale
after adding a series/movie. A larger library would want a DB-side `LIKE`/FTS query with paging,
or event-driven incremental index updates. Accepted for now.

### U13 — Low — accepted, debug-only — Diagnostics dump is large and logs are not scrubbed
`Diagnostics/DiagnosticsService.cs` writes **one text file** containing the whole server state —
both SQLite databases (every row, as JSON) and every application/update log file — so the file can
be tens of MB. `config.xml` secrets (API keys, OIDC/SSL/Postgres passwords, passphrases) and the
DB `Users.Password`/`Salt` + `Config` passphrase columns are redacted, but the **log text is
included verbatim** and may contain sensitive values (e.g. indexer URLs with keys). The endpoint
`GET /api/v3/system/diagnostics` is compiled only in **debug builds** and now requires normal API
authentication (it is no longer `[AllowAnonymous]`), so it is not reachable in release builds; keep
it debug-only. Postgres table enumeration is
basic (`pg_tables`, `public` schema). **If more detail is needed**, raise the log level
(`LogLevel` in Settings → General, or `config.xml`), reproduce, then download the dump: it already
includes `logs/theoriarr.txt`, `theoriarr.debug.txt`, `theoriarr.trace.txt` and `UpdateLogs/*`
whenever those files exist.

### U22 — Low — accepted tradeoff — fuzzy scene-alias matching can attach a release to a similarly named series
Anime/fansub releases sometimes name the series with an alias or a spaced/joined variant the exact
paths cannot see (e.g. a release titled after the series' scene alias rather than its library
title). `NzbDrone.Core/Parser/SeriesTitleMatcher.cs` (`ISeriesTitleMatcher`) adds a Lucene.NET fuzzy
fallback with two sources: scene **aliases** (match as adjacent-word n-grams of the parsed title,
normalised and compacted so a spaced alias matches its joined variant; accepted only when ≥ 8
characters and ≥ 1.5× the runner-up) and the series **title** (matched only against the whole parsed
title, ignoring a trailing year, so an exactly-named release like `KAKEGURUI TWIN` resolves the added
`Kakegurui Twin (2022)` without dragging in the parent `Kakegurui`). `ParsingService.FindSeries` runs
it only after exact title/id/scene-mapping resolution fails — restricted to the searched series (and
allowed to use the raw release title) during a search, and matching only the parser's own series-name
guess for RSS. Validated against a real diagnostics dump: of 609 previously-unresolved parsed titles
it resolves exactly 3, all correct (2 aliases, 1 whole-title/year-stripped); an alias-only variant
left the exact-title case unresolved, and a partial-title variant matched spin-off/sequel titles to
their parent series. Residual risk: two library series with the same year-stripped title are treated
as ambiguous (return null), and a spin-off/sequel whose alias equals a parent series' alias could
still be attached to the parent; raise `MinMatchLength` / `MinRunnerUpRatio` in
`SeriesTitleMatcher.cs` if it proves too loose.

### U23 — Low — accepted — a few tests use real (copyrighted) works from an out-of-repo fixture
Tests that validate against real-world naming reference real series/release titles. Those values
are kept out of the repository in an optional fixture file: `NzbDrone.Core.Test/Framework/ExternalTestFixtures.cs`
loads `theoriarr-test-fixtures.json` (path from `THEORIARR_TEST_FIXTURES`, else
`../theoriarr-test-fixtures.json` beside the repo root) — no file in git and the name is gitignored.
When the file is absent the affected tests pass without asserting. Add new real-world values to that
file (and the `ExternalTestFixtureData` model) rather than hard-coding them in tests.

### C1 — Low — accepted — transcode jobs do not resume across a restart
On startup `TranscodeService.RecoverInterruptedJobs` marks any `Running` job `Failed`
("Interrupted by a restart") and deletes its working folder; the original file is never touched.
A long encode therefore restarts from zero. Re-queue the file to retry.
(`src/Theoriarr.Series/src/NzbDrone.Core/MediaFiles/Transcoding/TranscodeService.cs`.)

### C2 — Low — accepted scope — QSV-native and AMD AMF are unsupported
Only NVIDIA NVENC, VA-API (AMD and Intel iGPUs alike — `VaapiBackend` enables any node whose
`vainfo` driver advertises an encode entrypoint) and the software encoders are enabled.
`TranscodeDeviceKind` reserves `Qsv`/`Amf`. QSV-specific options (`-qsv_device`, look-ahead) and
AMF (Windows-only) would need new `IHardwareAccelBackend`s without touching the scheduler. Note
that a VA-API node with no encode entrypoint (e.g. the NVIDIA NVDEC bridge, `VAEntrypointVLD`
only) is listed with `supported:false`/`enabled:false`.

### C3 — Low — accepted — HDR is not tonemapped yet
The device capability records a tonemap path (`opencl`/`vaapi`/`software`), but
`TranscodeArgumentBuilder` does not emit a tonemap filter, so an HDR source is passed through
(and, on a 10-bit encoder, stays HDR). Add `tonemap_opencl`/`tonemap_vaapi`/`tonemap` gated on
`DeviceCapability.Tonemap` when HDR→SDR is requested.

### C5 — Low — accepted — VA-API encodes use software decode + `hwupload`
`TranscodeArgumentBuilder.BuildVaapi` emits `-vaapi_device <node>` with
`-vf format=nv12,hwupload` (decode on CPU, upload to the GPU, encode on the GPU). This is fully
functional and avoids `-hwaccel_output_format vaapi` frame plumbing, at some CPU cost. Add
hardware decode later if CPU usage matters.

### C6 — Low — accepted — only the software target-size path is two-pass
Software target-size uses `-pass 1`/`-pass 2` for accurate sizing; NVENC and VA-API use
single-pass VBR (`buildNvidia`/`BuildVaapi`), which can overshoot on very short clips. Two-pass
can be added per backend if size accuracy becomes a complaint.

### C7 — Low — accepted — device `weight` is stored but not yet used for overflow
When `TranscodeEasiestJobsFirst` is on, `TranscodeService.BuildSchedule` spreads the free slots
round-robin across idle devices (hardest jobs to the most powerful, easiest to the least powerful)
so every device is used; the default sequential path fills the preferred device to its `maxParallel`
first. Either way `weight` is persisted/editable and still not consulted (plan §11 step 2). Wire
weighted distribution when a user wants a beefier device to take a proportionally larger share.

### C8 — Low — accepted — sidecar files are untouched (same name/extension is preserved)
Finalize keeps the media file's name and extension, so `.srt`/`.nfo` sidecars keep matching and
`ExtraService.MoveFilesAfterRename` is not needed. If a future option changes the extension, add
the rename hook (`TranscodeService.Resolve`).

### C9 — Low — accepted — the media-compression controller accepts either API key across domains
`MediaCompressionController` is dual-tagged `[AppSubsystem(Series)]` / `[AppSubsystem(Movies)]` and
does not branch on the requesting subsystem, so the series key and the movie key both list (and can
mutate) the unified job set, and `ToJobResources` enriches the whole list with both series and movie
titles. This is intentional for a single-listener unified app whose transcode page is one view; the
per-subsystem split would require separate queues per domain. The other unified controllers (Root
Folders/Tags/Quality Profiles) follow the same "visible to both keys" rule. Recorded so it is not
mistaken for an API-compat bug.

### Branding / identity — accepted

**What.** Product identity is Theoriarr; all upstream attribution, license notices and API-compat
surfaces are preserved (Sonarr/Radarr API shapes, `Sonarr.*` names/namespaces, upstream-notice
files).

**Why accepted.** A balanced rebrand: the product name and SPA are Theoriarr, while Prowlarr /
Jellyseerr / Overseerr compatibility and the upstream GPL notices are untouched. See the README
"Modification notice" for the legal framing.

### Architectural limitations (BY DESIGN)

- **Single download-client store** — one `DownloadClients` row shared by both domains (see the
  2026-09-23 fix). The unified design uses a **single shared category defaulting to `theoriarr`**;
  the media type of a download is determined by its grab history (authoritative for anything
  Theoriarr grabbed) and by title parsing for manually added downloads, never by the category.
  `TvCategory`/`MovieCategory` stay as Sonarr/Radarr-compatible API fields (the movie category is
  hidden in the UI and falls back to the shared one), and no distinct TV vs movie client rows exist
  (and none are needed for the single-client setup).
- **Single Prowlarr application** — register Theoriarr once (as Sonarr). Dual registration
  (separate Sonarr + Radarr apps) is unsupported: the name-uniqueness rule rejects it / both
  apps would stomp one shared indexer record. Movie categories can be added by putting the `2xxx`
  range in the single app's Sync Categories, but are no longer required: Theoriarr splits `2xxx` out
  at search time and falls back to Radarr's standard `2xxx` set when none are configured.
- **Jellyseerr must compare `appName`** — Theoriarr emits `appName` (in `/system/status`) and the
  `X-Application` response header, but the shared connection-test endpoints cannot reject a
  valid key belonging to the other domain because the client sends no domain hint. The remaining
  check belongs in `seerr/server/api/servarr/base.ts`; the 2026-09-21 audit confirmed seerr's
  current code still never reads `appName`, so a mis-keyed connection only fails later at
  `/series/lookup` or `/movie*` (404).
- **4K/HD require separate connections** — one movie per TMDB id with a single
  `qualityProfileId`; Jellyseerr's 4K/HD model assumes two Radarr instances/libraries.
- **Newznab API key is not required** — `requireApiKey` would break local Jackett/Prowlarr
  setups used without keys; 401/403 from an indexer is surfaced as `IndexerValidationInvalidApiKey`.
- **Auth / status-code edges** — absent or invalid API key → uniform **401**; a *valid*
  cross-domain key → **404** (by design). `/initialize.json` bootstrap is gated by the UI policy
  plus a loopback/RFC1918 requirement when auth is disabled. Prowlarr's optional HTTP Basic app
  credentials are a no-op (`X-Api-Key` still authenticates).
- **Shared unified version number** — `X-Application-Version` is the same for both domains (one
  assembly); `appName` still reflects the requesting domain. A distinct Radarr semantic version
  would require a separate build artifact.
- **Series-default shared rows are visible to both domains** — RootFolders/Tags/QualityProfiles
  whose stored `MediaType` is `Series(0)` (the column default applied by migration `311`/`315`,
  including defaults seeded at first run) are indistinguishable from "series-only". They stay
  visible to both keys (explicitly movie-created rows remain hidden from the series key); the
  movie list endpoints fall back to the unfiltered list rather than ever returning an empty list,
  so Jellyseerr can always configure a connection.
