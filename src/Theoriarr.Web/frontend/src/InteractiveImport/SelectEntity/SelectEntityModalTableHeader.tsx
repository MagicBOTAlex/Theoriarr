import classNames from 'classnames';
import React from 'react';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell, {
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
} from 'Components/Table/VirtualTableHeaderCell';
import { SelectEntityColumn } from './SelectEntity';

const COLUMN_CLASSES: Record<string, string> = {
  title: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[4_0_140px]'),
  year: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_70px]'),
  imdbId: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_110px]'),
  tmdbId: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_110px]'),
  tvdbId: classNames(VIRTUAL_TABLE_HEADER_CELL_CLASS, 'flex-[0_0_110px]'),
};

interface SelectEntityModalTableHeaderProps {
  columns: SelectEntityColumn[];
}

function SelectEntityModalTableHeader(
  props: SelectEntityModalTableHeaderProps
) {
  const { columns } = props;

  return (
    <VirtualTableHeader>
      {columns
        .filter((column) => column.isVisible !== false)
        .map((column) => {
          const { name, label } = column;

          return (
            <VirtualTableHeaderCell
              key={name}
              className={COLUMN_CLASSES[name]}
              name={name}
            >
              {typeof label === 'function' ? label() : label}
            </VirtualTableHeaderCell>
          );
        })}
    </VirtualTableHeader>
  );
}

export default SelectEntityModalTableHeader;
