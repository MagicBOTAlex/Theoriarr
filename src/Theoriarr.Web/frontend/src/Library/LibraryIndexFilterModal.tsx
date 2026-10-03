import React, { useCallback } from 'react';
import FilterModal, { FilterModalProps } from 'Components/Filter/FilterModal';
import { LIBRARY_FILTER_BUILDER } from './libraryFilters';
import { setLibraryOption } from './libraryOptionsStore';
import { MediaItem } from './MediaItem';
import useLibraryItems from './useLibraryItems';

type LibraryIndexFilterModalProps = FilterModalProps<MediaItem>;

export default function LibraryIndexFilterModal(
  props: LibraryIndexFilterModalProps
) {
  const { items } = useLibraryItems();

  const dispatchSetFilter = useCallback(
    ({ selectedFilterKey }: { selectedFilterKey: string | number }) => {
      setLibraryOption('selectedFilterKey', selectedFilterKey);
    },
    []
  );

  return (
    <FilterModal
      {...props}
      sectionItems={items}
      filterBuilderProps={LIBRARY_FILTER_BUILDER}
      customFilterType="library"
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
