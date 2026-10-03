import { maxBy } from 'lodash';
import { useMemo } from 'react';
import CommandNames from 'Commands/CommandNames';
import { useExecutingCommands } from 'Commands/useCommands';
import { useQualityProfile } from 'Settings/Profiles/Quality/useQualityProfiles';
import { MediaItem } from './MediaItem';

interface LibraryCommandBody {
  seriesId?: number;
  seriesIds?: number[];
  movieIds?: number[];
}

export function useLibraryIndexItem(item: MediaItem) {
  const executingCommands = useExecutingCommands();
  const qualityProfileId =
    item.series?.qualityProfileId ?? item.movie?.qualityProfileId;
  const qualityProfile = useQualityProfile(qualityProfileId);

  const isRefreshing = useMemo(() => {
    return executingCommands.some((command) => {
      const body = command.body as LibraryCommandBody | undefined;

      if (item.type === 'series') {
        return (
          command.name === CommandNames.RefreshSeries &&
          !!body?.seriesIds?.includes(item.id)
        );
      }

      return (
        command.name === CommandNames.RefreshMovie &&
        !!body?.movieIds?.includes(item.id)
      );
    });
  }, [executingCommands, item.type, item.id]);

  const isSearching = useMemo(() => {
    return executingCommands.some((command) => {
      const body = command.body as LibraryCommandBody | undefined;

      if (item.type === 'series') {
        return (
          command.name === CommandNames.SeriesSearch &&
          body?.seriesId === item.id
        );
      }

      return (
        command.name === CommandNames.MoviesSearch &&
        !!body?.movieIds?.includes(item.id)
      );
    });
  }, [executingCommands, item.type, item.id]);

  const latestSeason = useMemo(
    () => maxBy(item.series?.seasons ?? [], (season) => season.seasonNumber),
    [item.series]
  );

  return {
    qualityProfile,
    latestSeason,
    isRefreshing,
    isSearching,
  };
}

export default useLibraryIndexItem;
