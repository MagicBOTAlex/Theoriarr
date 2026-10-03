import { ServiceId } from 'Services';

// Media covers live on the single backend origin. Movie covers are namespaced
// so the merged backend can disambiguate movie artwork from series artwork
// without an API key (browser <img> requests carry none):
//
//     /MediaCover/movie/{id}/{filename}
//
// Series covers keep Sonarr's original `/MediaCover/{id}/{filename}` path (see
// `MediaCoverRoute.GetSeriesPath`). The API may emit either shape (optionally
// prefixed with the old `/movies` or `/series` base); both are normalized here
// to the path the backend actually serves for the requested media type.
export function getMediaCoverUrl(
  service: ServiceId,
  url?: string
): string | undefined {
  if (!url) {
    return url;
  }

  // Remote/absolute URLs (e.g. `remoteUrl`) are used verbatim.
  if (!url.startsWith('/')) {
    return url;
  }

  // Strip a legacy per-service base prefix (`/movies`, `/series`).
  const path = url.replace(/^\/(?:movies|series)(?=\/)/, '');
  const namespace = service === 'movies' ? 'movie/' : '';

  // Normalize `movie/`, `series/` or no namespace to the requested media type.
  return path.replace(
    /^\/MediaCover\/(?:(?:movie|series)\/)?/,
    `/MediaCover/${namespace}`
  );
}

export default getMediaCoverUrl;
