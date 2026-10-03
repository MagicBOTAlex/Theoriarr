import classNames from 'classnames';
import { throttle } from 'lodash';
import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { FixedSizeList as List, ListChildComponentProps } from 'react-window';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import ModalBody, { MODAL_BODY_CLASS } from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Scroller from 'Components/Scroller/Scroller';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import VirtualTableRowButton from 'Components/Table/VirtualTableRowButton';
import { scrollDirections } from 'Helpers/Props';
import dimensions from 'Styles/Variables/dimensions';
import { InputChanged } from 'typings/inputs';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import { SelectEntityColumn, SelectEntityItem } from './SelectEntity';
import SelectEntityModalTableHeader from './SelectEntityModalTableHeader';

const bodyPadding = parseInt(dimensions.pageContentBodyPadding);

const FILTER_INPUT_CLASS = classNames(INPUT_CLASS, 'flex-[0_0_auto] mb-[20px]');

const MODAL_BODY_EXTRA_CLASS = classNames(
  MODAL_BODY_CLASS,
  'flex flex-[1_1_auto] flex-col'
);

const SCROLLER_EXTRA_CLASS = 'flex-[1_1_auto]';

const COLUMN_CLASSES: Record<string, string> = {
  title: 'flex items-center flex-[4_0_140px]',
  year: 'flex items-center flex-[0_0_70px]',
  imdbId: 'flex items-center flex-[0_0_110px]',
  tmdbId: 'flex items-center flex-[0_0_110px]',
  tvdbId: 'flex items-center flex-[0_0_110px]',
};

interface RowData {
  items: SelectEntityItem[];
  columns: SelectEntityColumn[];
  onSelect(item: SelectEntityItem): void;
}

function Row({ index, style, data }: ListChildComponentProps<RowData>) {
  const { items, columns, onSelect } = data;
  const item = index >= items.length ? null : items[index];

  const handlePress = useCallback(() => {
    if (item != null) {
      onSelect(item);
    }
  }, [item, onSelect]);

  if (item == null) {
    return null;
  }

  return (
    <VirtualTableRowButton
      style={{
        display: 'flex',
        justifyContent: 'space-between',
        ...style,
      }}
      onPress={handlePress}
    >
      {columns
        .filter((column) => column.isVisible !== false)
        .map((column) => {
          return (
            <VirtualTableRowCell
              key={column.name}
              className={COLUMN_CLASSES[column.name]}
            >
              {column.render(item)}
            </VirtualTableRowCell>
          );
        })}
    </VirtualTableRowButton>
  );
}

interface SelectEntityModalContentProps<T extends SelectEntityItem> {
  title: string;
  filterPlaceholder: string;
  columns: SelectEntityColumn<T>[];
  items: readonly T[];
  getSearchValues(item: T): string[];
  onSelect(item: T): void;
  onModalClose(): void;
}

function SelectEntityModalContent<T extends SelectEntityItem>(
  props: SelectEntityModalContentProps<T>
) {
  const {
    title,
    filterPlaceholder,
    columns,
    items,
    getSearchValues,
    onSelect,
    onModalClose,
  } = props;

  const listRef = useRef<List<RowData>>(null);
  const scrollerRef = useRef<HTMLDivElement>(null);
  const [filter, setFilter] = useState('');
  const [size, setSize] = useState({ width: 0, height: 0 });
  const windowHeight = window.innerHeight;

  useEffect(() => {
    const current = scrollerRef?.current as HTMLElement;

    if (current) {
      const width = current.clientWidth;
      const height = current.clientHeight;
      const padding = bodyPadding - 5;

      setSize({
        width: width - padding * 2,
        height: height + padding,
      });
    }
  }, [windowHeight, scrollerRef]);

  useEffect(() => {
    const currentScrollerRef = scrollerRef.current as HTMLElement;
    const currentScrollListener = currentScrollerRef;

    const handleScroll = throttle(() => {
      const { offsetTop = 0 } = currentScrollerRef;
      const scrollTop = currentScrollerRef.scrollTop - offsetTop;

      listRef.current?.scrollTo(scrollTop);
    }, 10);

    currentScrollListener.addEventListener('scroll', handleScroll);

    return () => {
      handleScroll.cancel();

      if (currentScrollListener) {
        currentScrollListener.removeEventListener('scroll', handleScroll);
      }
    };
  }, [listRef, scrollerRef]);

  const onFilterChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setFilter(value);
    },
    [setFilter]
  );

  const sortedItems = useMemo(
    () =>
      [...items].sort(sortByProp<SelectEntityItem, 'sortTitle'>('sortTitle')),
    [items]
  );

  const filteredItems = useMemo(() => {
    const filterLower = filter.toLowerCase();

    return sortedItems.filter((item) => {
      return getSearchValues(item).some((value) => {
        return value.toLowerCase().includes(filterLower);
      });
    });
  }, [sortedItems, filter, getSearchValues]);

  const itemData = useMemo<RowData>(
    () => ({
      items: filteredItems as SelectEntityItem[],
      columns: columns as unknown as SelectEntityColumn[],
      onSelect: onSelect as (item: SelectEntityItem) => void,
    }),
    [filteredItems, columns, onSelect]
  );

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{title}</ModalHeader>

      <ModalBody
        className={MODAL_BODY_EXTRA_CLASS}
        scrollDirection={scrollDirections.NONE}
      >
        <TextInput
          className={FILTER_INPUT_CLASS}
          placeholder={filterPlaceholder}
          name="filter"
          value={filter}
          autoFocus={true}
          onChange={onFilterChange}
        />

        <Scroller
          ref={scrollerRef}
          className={SCROLLER_EXTRA_CLASS}
          autoFocus={false}
        >
          <SelectEntityModalTableHeader
            columns={columns as unknown as SelectEntityColumn[]}
          />
          <List<RowData>
            ref={listRef}
            style={{
              width: '100%',
              height: '100%',
              overflow: 'none',
            }}
            width={size.width}
            height={size.height}
            itemCount={filteredItems.length}
            itemSize={38}
            itemData={itemData}
          >
            {Row}
          </List>
        </Scroller>
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Cancel')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SelectEntityModalContent;
