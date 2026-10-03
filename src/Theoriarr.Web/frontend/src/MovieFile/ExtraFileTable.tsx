import React from 'react';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import { ExtraFile } from './ExtraFile';

const columns: Column[] = [
  {
    name: 'relativePath',
    label: () => translate('RelativePath'),
    isVisible: true,
  },
  {
    name: 'extension',
    label: () => translate('Extension'),
    isVisible: true,
  },
  {
    name: 'type',
    label: () => translate('Type'),
    isVisible: true,
  },
];

const DEFAULT_EXTRA_FILES: ExtraFile[] = [];

interface ExtraFileTableProps {
  movieId: number;
}

function ExtraFileTable({ movieId }: ExtraFileTableProps) {
  const { data } = useApiQuery<ExtraFile[]>({
    service: 'movies',
    path: '/extrafile',
    queryParams: { movieId },
  });

  const items = data ?? DEFAULT_EXTRA_FILES;

  return (
    <div className="mt-[20px] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--inputBackgroundColor)] [&:last-of-type]:mb-0">
      {items.length ? null : (
        <div className="pt-[10px] pb-[10px] pl-[2em]">
          {translate('NoExtraFilesToManage')}
        </div>
      )}

      {items.length ? (
        <Table columns={columns}>
          <TableBody>
            {items.map((item) => {
              return (
                <TableRow key={item.id}>
                  <TableRowCell title={item.relativePath} className="break-all">
                    {item.relativePath}
                  </TableRowCell>

                  <TableRowCell title={item.extension}>
                    {item.extension}
                  </TableRowCell>

                  <TableRowCell title={item.type}>
                    {titleCase(item.type)}
                  </TableRowCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      ) : null}
    </div>
  );
}

export default ExtraFileTable;
