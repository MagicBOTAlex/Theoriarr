# Theoriarr TODO

Forward plan (updated 2026-10-03). Metadata is served by
[Providarr](https://github.com/MagicBOTAlex/Providarr) (see `Providarr/PROGRESS.md`). Development
history lives in `git log`; long-form accepted limitations live in [`DEFERRED.md`](DEFERRED.md).

Legend: `[ ]` open · `[~]` in progress. Citations are `path:line` where useful.

---

## OAuth / auth (self-hostable — GPL-3.0)
`Servarr/ServarrAPI.Auth` is the `auth.servarr.com` broker, GPL-3.0.

> **Remaining Servarr-operated endpoints.** After removing `services.sonarr.tv` and the unused
> `radarr.servarr.com` factory, the **only** remaining Servarr servers are the OAuth broker
> (`auth.servarr.com` for Trakt/Simkl/AniList) and the MyAnimeList OAuth paths
> (`services.sonarr.tv/oauth/myanimelist/*`). These integrations are now **intentionally disabled**
> (see below) and disclosed in the README.

- [ ] Self-host a `ServarrAPI.Auth` instance (preserve GPL attribution/license) and make the broker
      base URL configurable so both the `auth.servarr.com` constants and the MyAnimeList OAuth paths
      can be repointed. Then set `ServarrAuthDependencies.Enabled = true`.
- [ ] Register our own **Trakt / Simkl / AniList** apps pointing at it.
- [ ] Repoint the `auth.servarr.com` constants in `ImportLists/{Trakt,Simkl,AniList}` and
      `Notifications/Trakt/TraktProxy.cs`.
- [ ] **Pushover:** register our own Pushover app token; replace
      `Datastore/Migration/033_add_api_key_to_pushover.cs` and the `PushoverSettings` help link.
