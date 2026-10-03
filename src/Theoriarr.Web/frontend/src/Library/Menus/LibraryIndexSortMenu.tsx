import React from 'react';
import MenuContent from 'Components/Menu/MenuContent';
import SortMenu from 'Components/Menu/SortMenu';
import SortMenuItem from 'Components/Menu/SortMenuItem';
import { align } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import translate from 'Utilities/String/translate';

interface LibraryIndexSortMenuProps {
  sortKey?: string;
  sortDirection?: SortDirection;
  isDisabled: boolean;
  onSortSelect(sortKey: string): void;
}

function LibraryIndexSortMenu(props: LibraryIndexSortMenuProps) {
  const { sortKey, sortDirection, isDisabled, onSortSelect } = props;

  const itemProps = { sortKey, sortDirection, onPress: onSortSelect };

  return (
    <SortMenu isDisabled={isDisabled} alignMenu={align.RIGHT}>
      <MenuContent>
        <SortMenuItem name="status" {...itemProps}>
          {translate('MonitoredStatus')}
        </SortMenuItem>

        <SortMenuItem name="movieReleaseStatus" {...itemProps}>
          {translate('ReleaseStatus')}
        </SortMenuItem>

        <SortMenuItem name="movieStatus" {...itemProps}>
          {translate('Status')}
        </SortMenuItem>

        <SortMenuItem name="sortTitle" {...itemProps}>
          {translate('Title')}
        </SortMenuItem>

        <SortMenuItem name="originalTitle" {...itemProps}>
          {translate('OriginalTitle')}
        </SortMenuItem>

        <SortMenuItem name="collection" {...itemProps}>
          {translate('Collection')}
        </SortMenuItem>

        <SortMenuItem name="network" {...itemProps}>
          {translate('Network')}
        </SortMenuItem>

        <SortMenuItem name="studio" {...itemProps}>
          {translate('Studio')}
        </SortMenuItem>

        <SortMenuItem name="originalCountry" {...itemProps}>
          {translate('OriginalCountry')}
        </SortMenuItem>

        <SortMenuItem name="originalLanguage" {...itemProps}>
          {translate('OriginalLanguage')}
        </SortMenuItem>

        <SortMenuItem name="qualityProfileId" {...itemProps}>
          {translate('QualityProfile')}
        </SortMenuItem>

        <SortMenuItem name="nextAiring" {...itemProps}>
          {translate('NextAiring')}
        </SortMenuItem>

        <SortMenuItem name="previousAiring" {...itemProps}>
          {translate('PreviousAiring')}
        </SortMenuItem>

        <SortMenuItem name="added" {...itemProps}>
          {translate('Added')}
        </SortMenuItem>

        <SortMenuItem name="seasonCount" {...itemProps}>
          {translate('Seasons')}
        </SortMenuItem>

        <SortMenuItem name="episodeProgress" {...itemProps}>
          {translate('Episodes')}
        </SortMenuItem>

        <SortMenuItem name="episodeCount" {...itemProps}>
          {translate('EpisodeCount')}
        </SortMenuItem>

        <SortMenuItem name="latestSeason" {...itemProps}>
          {translate('LatestSeason')}
        </SortMenuItem>

        <SortMenuItem name="inCinemas" {...itemProps}>
          {translate('InCinemas')}
        </SortMenuItem>

        <SortMenuItem name="digitalRelease" {...itemProps}>
          {translate('DigitalRelease')}
        </SortMenuItem>

        <SortMenuItem name="physicalRelease" {...itemProps}>
          {translate('PhysicalRelease')}
        </SortMenuItem>

        <SortMenuItem name="releaseDate" {...itemProps}>
          {translate('ReleaseDate')}
        </SortMenuItem>

        <SortMenuItem name="runtime" {...itemProps}>
          {translate('Runtime')}
        </SortMenuItem>

        <SortMenuItem name="path" {...itemProps}>
          {translate('Path')}
        </SortMenuItem>

        <SortMenuItem name="sizeOnDisk" {...itemProps}>
          {translate('SizeOnDisk')}
        </SortMenuItem>

        <SortMenuItem name="averageSizePerEpisode" {...itemProps}>
          {translate('AverageSizePerEpisode')}
        </SortMenuItem>

        <SortMenuItem name="tmdbRating" {...itemProps}>
          {translate('TmdbRating')}
        </SortMenuItem>

        <SortMenuItem name="imdbRating" {...itemProps}>
          {translate('ImdbRating')}
        </SortMenuItem>

        <SortMenuItem name="rottenTomatoesRating" {...itemProps}>
          {translate('RottenTomatoesRating')}
        </SortMenuItem>

        <SortMenuItem name="traktRating" {...itemProps}>
          {translate('TraktRating')}
        </SortMenuItem>

        <SortMenuItem name="popularity" {...itemProps}>
          {translate('Popularity')}
        </SortMenuItem>

        <SortMenuItem name="tags" {...itemProps}>
          {translate('Tags')}
        </SortMenuItem>

        <SortMenuItem name="ratings" {...itemProps}>
          {translate('Rating')}
        </SortMenuItem>
      </MenuContent>
    </SortMenu>
  );
}

export default LibraryIndexSortMenu;
