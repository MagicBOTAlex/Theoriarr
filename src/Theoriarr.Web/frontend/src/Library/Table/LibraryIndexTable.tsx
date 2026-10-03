import React, { RefObject, useEffect, useMemo, useRef } from 'react';
import { FixedSizeList, ListChildComponentProps } from 'react-window';
import Column from 'Components/Table/Column';
import VirtualTable from 'Components/Table/VirtualTable';
import { SortDirection } from 'Helpers/Props/sortDirections';
import getIndexOfFirstCharacter from 'Utilities/Array/getIndexOfFirstCharacter';
import {
  useLibraryOption,
  useLibraryTableOptions,
} from '../libraryOptionsStore';
import { MediaItem } from '../MediaItem';
import LibraryIndexRow from './LibraryIndexRow';
import LibraryIndexTableHeader from './LibraryIndexTableHeader';

interface RowItemData {
  items: MediaItem[];
  sortKey: string;
  columns: Column[];
  isSelectMode: boolean;
}

interface LibraryIndexTableProps {
  items: MediaItem[];
  sortKey: string;
  sortDirection?: SortDirection;
  jumpToCharacter?: string;
  scrollTop?: number;
  scrollerRef: RefObject<HTMLElement>;
  isSelectMode: boolean;
  isSmallScreen: boolean;
}

function Row({ index, style, data }: ListChildComponentProps<RowItemData>) {
  const { items, sortKey, columns, isSelectMode } = data;

  if (index >= items.length) {
    return null;
  }

  const item = items[index];

  return (
    <div
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        ...style,
      }}
      className="transition-colors duration-500 hover:bg-[var(--tableRowHoverBackgroundColor)]"
    >
      <LibraryIndexRow
        item={item}
        sortKey={sortKey}
        columns={columns}
        isSelectMode={isSelectMode}
      />
    </div>
  );
}

function LibraryIndexTable({
  items,
  sortKey,
  sortDirection,
  jumpToCharacter,
  isSelectMode,
  isSmallScreen,
  scrollerRef,
}: LibraryIndexTableProps) {
  const columns = useLibraryOption('columns');
  const { showBanners } = useLibraryTableOptions();
  const listRef = useRef<FixedSizeList<RowItemData>>(null);

  const rowHeight = useMemo(() => {
    return showBanners ? 70 : 38;
  }, [showBanners]);

  useEffect(() => {
    if (jumpToCharacter) {
      const index = getIndexOfFirstCharacter(items, jumpToCharacter);

      if (index != null) {
        let scrollTop = index * rowHeight;

        if (scrollTop > 0) {
          const offset = 57;

          scrollTop += offset;
        }

        listRef.current?.scrollTo(scrollTop);
        scrollerRef?.current?.scrollTo(0, scrollTop);
      }
    }
  }, [jumpToCharacter, rowHeight, items, scrollerRef, listRef]);

  return (
    <VirtualTable
      Header={
        <LibraryIndexTableHeader
          showBanners={showBanners}
          columns={columns}
          sortKey={sortKey}
          sortDirection={sortDirection}
          isSelectMode={isSelectMode}
        />
      }
      itemCount={items.length}
      itemData={{
        items,
        sortKey,
        columns,
        isSelectMode,
      }}
      isSmallScreen={isSmallScreen}
      listRef={listRef}
      rowHeight={rowHeight}
      Row={Row}
      scrollerRef={scrollerRef}
    />
  );
}

export default LibraryIndexTable;
