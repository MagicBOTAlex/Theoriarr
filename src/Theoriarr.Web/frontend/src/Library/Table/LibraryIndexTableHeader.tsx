import classNames from 'classnames';
import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import IconButton from 'Components/Link/IconButton';
import Column from 'Components/Table/Column';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell, {
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
} from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableSelectAllHeaderCell from 'Components/Table/VirtualTableSelectAllHeaderCell';
import { icons } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { CheckInputChanged } from 'typings/inputs';
import { TableOptionsChangePayload } from 'typings/Table';
import translate from 'Utilities/String/translate';
import {
  setLibraryOption,
  setLibrarySort,
  setLibraryTableOptions,
} from '../libraryOptionsStore';
import hasGrowableColumns from './hasGrowableColumns';
import LibraryIndexTableOptions from './LibraryIndexTableOptions';

const COLUMN_CLASSES: Record<string, string> = {
  status: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_60px]'),
  sortTitle: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[4_0_110px]'),
  originalTitle: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[4_0_110px]'
  ),
  collection: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[4_0_110px]'),
  seriesType: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_100px]'),
  network: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[2_0_90px]'),
  studio: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[2_0_90px]'),
  qualityProfileId: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[1_0_125px]'
  ),
  nextAiring: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  previousAiring: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_180px]'
  ),
  originalCountry: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[1_0_125px]'
  ),
  originalLanguage: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[1_0_125px]'
  ),
  added: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  year: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_80px]'),
  inCinemas: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  digitalRelease: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_180px]'
  ),
  physicalRelease: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_180px]'
  ),
  releaseDate: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  runtime: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_100px]'),
  minimumAvailability: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_140px]'
  ),
  seasonCount: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_100px]'),
  seasonFolder: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_150px]'),
  episodeProgress: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_150px]'
  ),
  episodeCount: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_130px]'),
  latestSeason: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_150px]'),
  path: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[1_0_150px]'),
  sizeOnDisk: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_120px]'),
  averageSizePerEpisode: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_160px]'
  ),
  genres: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  keywords: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  movieStatus: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_150px]'),
  tmdbRating: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_80px]'),
  imdbRating: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_80px]'),
  rottenTomatoesRating: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_80px]'
  ),
  traktRating: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_80px]'),
  popularity: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_100px]'),
  ratings: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_80px]'),
  certification: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_100px]'
  ),
  releaseGroups: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_180px]'
  ),
  releaseTypes: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_180px]'),
  episodeFileQualities: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_220px]'
  ),
  movieFileQualities: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_220px]'
  ),
  tags: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[1_0_60px]'),
  useSceneNumbering: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_145px]'
  ),
  monitorNewItems: classNames(
    VIRTUAL_TABLE_HEADER_CELL_CLASS,
    'flex-[0_0_175px]'
  ),
  actions: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_1_90px]'),
};

const BANNER_CLASS = 'flex-[0_0_379px]!';
const BANNER_GROW_CLASS = 'grow';

interface LibraryIndexTableHeaderProps {
  showBanners: boolean;
  columns: Column[];
  sortKey?: string;
  sortDirection?: SortDirection;
  isSelectMode: boolean;
}

function LibraryIndexTableHeader(props: LibraryIndexTableHeaderProps) {
  const { showBanners, columns, sortKey, sortDirection, isSelectMode } = props;
  const { allSelected, allUnselected, selectAll, unselectAll } = useSelect();

  const onSortPress = useCallback(
    (sortKey: string, sortDirection?: SortDirection) => {
      setLibrarySort({ sortKey, sortDirection });
    },
    []
  );

  const onTableOptionChange = useCallback(
    (
      payload: TableOptionsChangePayload & {
        tableOptions?: { showBanners?: boolean; showSearchAction?: boolean };
      }
    ) => {
      if (payload.tableOptions) {
        setLibraryTableOptions(payload.tableOptions);
      } else if (payload.columns) {
        setLibraryOption('columns', payload.columns);
      }
    },
    []
  );

  const onSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      if (value) {
        selectAll();
      } else {
        unselectAll();
      }
    },
    [selectAll, unselectAll]
  );

  return (
    <VirtualTableHeader>
      {isSelectMode ? (
        <VirtualTableSelectAllHeaderCell
          allSelected={allSelected}
          allUnselected={allUnselected}
          onSelectAllChange={onSelectAllChange}
        />
      ) : null}

      {columns.map((column) => {
        const { name, label, isSortable, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'actions') {
          return (
            <VirtualTableHeaderCell
              key={name}
              className={COLUMN_CLASSES[name]}
              name={name}
              isSortable={false}
            >
              <TableOptionsModalWrapper
                columns={columns}
                optionsComponent={LibraryIndexTableOptions}
                onTableOptionChange={onTableOptionChange}
              >
                <IconButton
                  name={icons.ADVANCED_SETTINGS}
                  aria-label={translate('AdvancedSettings')}
                />
              </TableOptionsModalWrapper>
            </VirtualTableHeaderCell>
          );
        }

        return (
          <VirtualTableHeaderCell
            key={name}
            className={classNames(
              COLUMN_CLASSES[name],
              name === 'sortTitle' && showBanners && BANNER_CLASS,
              name === 'sortTitle' &&
                showBanners &&
                !hasGrowableColumns(columns) &&
                BANNER_GROW_CLASS
            )}
            name={name}
            sortKey={sortKey}
            sortDirection={sortDirection}
            isSortable={isSortable}
            onSortPress={onSortPress}
          >
            {typeof label === 'function' ? label() : label}
          </VirtualTableHeaderCell>
        );
      })}
    </VirtualTableHeader>
  );
}

export default LibraryIndexTableHeader;
