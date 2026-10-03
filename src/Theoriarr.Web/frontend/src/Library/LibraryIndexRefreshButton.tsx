import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { splitSelectKeys } from './MediaItem';
import useLibraryIndex from './useLibraryIndex';

interface LibraryIndexRefreshButtonProps {
  isSelectMode: boolean;
  selectedFilterKey: string | number;
}

function LibraryIndexRefreshButton(props: LibraryIndexRefreshButtonProps) {
  const isRefreshingSeries = useCommandExecuting(CommandNames.RefreshSeries);
  const isRefreshingMovies = useCommandExecuting(CommandNames.RefreshMovie);
  const isRefreshing = isRefreshingSeries || isRefreshingMovies;

  const { data, totalItems } = useLibraryIndex();
  const executeCommand = useExecuteCommand();
  const { isSelectMode, selectedFilterKey } = props;
  const { anySelected, getSelectedIds } = useSelect();

  let refreshLabel = translate('UpdateAll');

  if (anySelected) {
    refreshLabel = translate('UpdateSelected');
  } else if (selectedFilterKey !== 'all') {
    refreshLabel = translate('UpdateFiltered');
  }

  const onPress = useCallback(() => {
    const selectKeys =
      isSelectMode && anySelected
        ? getSelectedIds()
        : data.map((item) => item.selectKey);

    const { seriesIds, movieIds } = splitSelectKeys(selectKeys);

    if (seriesIds.length) {
      executeCommand({
        name: CommandNames.RefreshSeries,
        seriesIds,
      });
    }

    if (movieIds.length) {
      executeCommand({
        name: CommandNames.RefreshMovie,
        movieIds,
      });
    }
  }, [executeCommand, anySelected, isSelectMode, data, getSelectedIds]);

  return (
    <PageToolbarButton
      label={refreshLabel}
      isSpinning={isRefreshing}
      isDisabled={!totalItems}
      iconName={icons.REFRESH}
      onPress={onPress}
    />
  );
}

export default LibraryIndexRefreshButton;
