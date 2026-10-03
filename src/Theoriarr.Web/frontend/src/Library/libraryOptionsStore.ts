import Column from 'Components/Table/Column';
import { createOptionsStore } from 'Helpers/Hooks/useOptionsStore';
import translate from 'Utilities/String/translate';

export interface LibraryOptions {
  selectedFilterKey: string | number;
  sortKey: string;
  sortDirection: 'ascending' | 'descending';
  view: string;
  search: string;
  columns: Column[];
  posterOptions: {
    detailedProgressBar: boolean;
    size: 'small' | 'medium' | 'large';
    showTitle: boolean;
    showMonitored: boolean;
    showQualityProfile: boolean;
    showCinemaRelease: boolean;
    showDigitalRelease: boolean;
    showPhysicalRelease: boolean;
    showReleaseDate: boolean;
    showTmdbRating: boolean;
    showImdbRating: boolean;
    showRottenTomatoesRating: boolean;
    showTraktRating: boolean;
    showTags: boolean;
    showSearchAction: boolean;
  };
  overviewOptions: {
    detailedProgressBar: boolean;
    size: 'small' | 'medium' | 'large';
    showMonitored: boolean;
    showNetwork: boolean;
    showStudio: boolean;
    showCollection: boolean;
    showQualityProfile: boolean;
    showPreviousAiring: boolean;
    showAdded: boolean;
    showSeasonCount: boolean;
    showCinemaRelease: boolean;
    showDigitalRelease: boolean;
    showPhysicalRelease: boolean;
    showReleaseDate: boolean;
    showRuntime: boolean;
    showPath: boolean;
    showSizeOnDisk: boolean;
    showTags: boolean;
    showSearchAction: boolean;
  };
  tableOptions: {
    showBanners: boolean;
    showSearchAction: boolean;
  };
  deleteOptions: {
    addImportListExclusion: boolean;
    addImportExclusion: boolean;
  };
}

const { useOptions, useOption, setOptions, setOption, setSort, getOptions } =
  createOptionsStore<LibraryOptions>('library_options', () => {
    return {
      selectedFilterKey: 'all',
      sortKey: 'sortTitle',
      sortDirection: 'ascending',
      view: 'posters',
      search: '',
      posterOptions: {
        detailedProgressBar: false,
        size: 'large',
        showTitle: false,
        showMonitored: true,
        showQualityProfile: true,
        showCinemaRelease: false,
        showDigitalRelease: false,
        showPhysicalRelease: false,
        showReleaseDate: false,
        showTmdbRating: false,
        showImdbRating: false,
        showRottenTomatoesRating: false,
        showTraktRating: false,
        showTags: false,
        showSearchAction: false,
      },
      overviewOptions: {
        detailedProgressBar: false,
        size: 'medium',
        showMonitored: true,
        showNetwork: true,
        showStudio: true,
        showCollection: false,
        showQualityProfile: true,
        showPreviousAiring: false,
        showAdded: false,
        showSeasonCount: true,
        showCinemaRelease: false,
        showDigitalRelease: false,
        showPhysicalRelease: false,
        showReleaseDate: false,
        showRuntime: false,
        showPath: false,
        showSizeOnDisk: false,
        showTags: false,
        showSearchAction: false,
      },
      tableOptions: {
        showBanners: false,
        showSearchAction: false,
      },
      deleteOptions: {
        addImportListExclusion: false,
        addImportExclusion: false,
      },
      columns: [
        {
          name: 'status',
          label: '',
          columnLabel: () => translate('Status'),
          isSortable: true,
          isVisible: true,
          isModifiable: 'disabled',
        },
        {
          name: 'sortTitle',
          label: () => translate('Title'),
          isSortable: true,
          isVisible: true,
          isModifiable: 'disabled',
        },
        {
          name: 'originalTitle',
          label: () => translate('OriginalTitle'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'collection',
          label: () => translate('Collection'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'seriesType',
          label: () => translate('Type'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'network',
          label: () => translate('Network'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'studio',
          label: () => translate('Studio'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'qualityProfileId',
          label: () => translate('QualityProfile'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'nextAiring',
          label: () => translate('NextAiring'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'previousAiring',
          label: () => translate('PreviousAiring'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'originalCountry',
          label: () => translate('OriginalCountry'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'originalLanguage',
          label: () => translate('OriginalLanguage'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'added',
          label: () => translate('Added'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'year',
          label: () => translate('Year'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'inCinemas',
          label: () => translate('InCinemas'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'digitalRelease',
          label: () => translate('DigitalRelease'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'physicalRelease',
          label: () => translate('PhysicalRelease'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'releaseDate',
          label: () => translate('ReleaseDate'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'runtime',
          label: () => translate('Runtime'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'minimumAvailability',
          label: () => translate('MinimumAvailability'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'seasonCount',
          label: () => translate('Seasons'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'seasonFolder',
          label: () => translate('SeasonFolder'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'episodeProgress',
          label: () => translate('Episodes'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'episodeCount',
          label: () => translate('EpisodeCount'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'latestSeason',
          label: () => translate('LatestSeason'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'path',
          label: () => translate('Path'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'sizeOnDisk',
          label: () => translate('SizeOnDisk'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'averageSizePerEpisode',
          label: () => translate('AverageSize'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'genres',
          label: () => translate('Genres'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'keywords',
          label: () => translate('Keywords'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'movieStatus',
          label: () => translate('Status'),
          isSortable: true,
          isVisible: true,
        },
        {
          name: 'tmdbRating',
          label: () => translate('TmdbRating'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'imdbRating',
          label: () => translate('ImdbRating'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'rottenTomatoesRating',
          label: () => translate('RottenTomatoesRating'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'traktRating',
          label: () => translate('TraktRating'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'popularity',
          label: () => translate('Popularity'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'ratings',
          label: () => translate('Rating'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'certification',
          label: () => translate('Certification'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'releaseGroups',
          label: () => translate('ReleaseGroups'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'releaseTypes',
          label: () => translate('ReleaseTypes'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'episodeFileQualities',
          label: () => translate('EpisodeFileQualities'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'movieFileQualities',
          label: () => translate('MovieFileQualities'),
          isSortable: false,
          isVisible: false,
        },
        {
          name: 'tags',
          label: () => translate('Tags'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'useSceneNumbering',
          label: () => translate('SceneNumbering'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'monitorNewItems',
          label: () => translate('MonitorNewSeasons'),
          isSortable: true,
          isVisible: false,
        },
        {
          name: 'actions',
          label: '',
          columnLabel: () => translate('Actions'),
          isVisible: true,
          isModifiable: 'disabled',
        },
      ],
    };
  });

export const useLibraryOptions = useOptions;
export const setLibraryOptions = setOptions;
export const useLibraryOption = useOption;
export const setLibraryOption = setOption;
export const setLibrarySort = setSort;

export const useLibraryPosterOptions = () => useOption('posterOptions');

export const setLibraryPosterOptions = (
  options: Partial<LibraryOptions['posterOptions']>
) => {
  const currentOptions = getOptions().posterOptions;
  setLibraryOption('posterOptions', { ...currentOptions, ...options });
};

export const useLibraryOverviewOptions = () => useOption('overviewOptions');

export const setLibraryOverviewOptions = (
  options: Partial<LibraryOptions['overviewOptions']>
) => {
  const currentOptions = getOptions().overviewOptions;
  setLibraryOption('overviewOptions', { ...currentOptions, ...options });
};

export const useLibraryTableOptions = () => useOption('tableOptions');

export const setLibraryTableOptions = (
  options: Partial<LibraryOptions['tableOptions']>
) => {
  const currentOptions = getOptions().tableOptions;
  setLibraryOption('tableOptions', { ...currentOptions, ...options });
};

export const useLibraryDeleteOptions = () => useOption('deleteOptions');

export const setLibraryDeleteOptions = (
  options: Partial<LibraryOptions['deleteOptions']>
) => {
  const currentOptions = getOptions().deleteOptions;
  setLibraryOption('deleteOptions', { ...currentOptions, ...options });
};
