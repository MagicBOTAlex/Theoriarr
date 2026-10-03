interface EpisodeSearchPayload {
  episodeId: number;
}

interface SeasonSearchPayload {
  seriesId: number;
  seasonNumber: number;
}

interface MovieSearchPayload {
  movieId: number;
}

type InteractiveSearchPayload =
  | EpisodeSearchPayload
  | SeasonSearchPayload
  | MovieSearchPayload;

export default InteractiveSearchPayload;
