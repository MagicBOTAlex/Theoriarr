import React, { useMemo } from 'react';
import MediaGrid from 'Library/MediaGrid';
import { MediaItem, movieToMediaItem } from 'Library/MediaItem';
import { Movie } from 'Movies/Movie';

interface MovieGridProps {
  movies: readonly Movie[];
  emptyMessage?: string;
  getLink?: (movie: Movie) => string;
  getSubtitle?: (movie: Movie) => string;
}

function MovieGrid({
  movies,
  emptyMessage = 'Nothing to show',
  getLink,
  getSubtitle,
}: MovieGridProps) {
  const items = useMemo(() => movies.map(movieToMediaItem), [movies]);

  const getItemLink = useMemo(() => {
    if (!getLink) {
      return undefined;
    }

    return (item: MediaItem) => getLink(item.movie as Movie);
  }, [getLink]);

  const getItemSubtitle = useMemo(() => {
    if (!getSubtitle) {
      return undefined;
    }

    return (item: MediaItem) => getSubtitle(item.movie as Movie);
  }, [getSubtitle]);

  return (
    <MediaGrid
      items={items}
      emptyMessage={emptyMessage}
      getLink={getItemLink}
      getSubtitle={getItemSubtitle}
    />
  );
}

export default MovieGrid;
