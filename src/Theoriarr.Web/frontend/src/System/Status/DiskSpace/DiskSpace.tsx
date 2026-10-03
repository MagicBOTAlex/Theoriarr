import React, { useCallback, useState } from 'react';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ProgressBar from 'Components/ProgressBar';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRowButton from 'Components/Table/TableRowButton';
import { kinds, sizes } from 'Helpers/Props';
import { Kind } from 'Helpers/Props/kinds';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import StorageUsageModal from './StorageUsageModal';
import useDiskSpace from './useDiskSpace';

interface SelectedDisk {
  path: string;
  label?: string;
}

interface DiskSpaceRowProps {
  path: string;
  label?: string;
  freeSpace: number;
  totalSpace: number;
  onRowPress: (disk: SelectedDisk) => void;
}

const columns: Column[] = [
  {
    name: 'path',
    label: () => translate('Location'),
    isVisible: true,
  },
  {
    name: 'freeSpace',
    label: () => translate('FreeSpace'),
    isVisible: true,
  },
  {
    name: 'totalSpace',
    label: () => translate('TotalSpace'),
    isVisible: true,
  },
  {
    name: 'progress',
    label: '',
    isVisible: true,
  },
];

function DiskSpaceRow({
  path,
  label,
  freeSpace,
  totalSpace,
  onRowPress,
}: DiskSpaceRowProps) {
  const handlePress = useCallback(() => {
    onRowPress({ path, label });
  }, [onRowPress, path, label]);

  const diskUsage = 100 - (freeSpace / totalSpace) * 100;
  let diskUsageKind: Kind = 'primary';

  if (diskUsage > 90) {
    diskUsageKind = kinds.DANGER;
  } else if (diskUsage > 80) {
    diskUsageKind = kinds.WARNING;
  }

  return (
    <TableRowButton onPress={handlePress}>
      <TableRowCell>
        {path}

        {label && ` (${label})`}
      </TableRowCell>

      <TableRowCell className="w-[150px]">
        {formatBytes(freeSpace)}
      </TableRowCell>

      <TableRowCell className="w-[150px]">
        {formatBytes(totalSpace)}
      </TableRowCell>

      <TableRowCell className="w-[150px]">
        <ProgressBar
          progress={diskUsage}
          kind={diskUsageKind}
          size={sizes.MEDIUM}
        />
      </TableRowCell>
    </TableRowButton>
  );
}

function DiskSpace() {
  const { isFetching, data } = useDiskSpace();
  const [selectedDisk, setSelectedDisk] = useState<SelectedDisk | null>(null);

  const handleRowPress = useCallback((disk: SelectedDisk) => {
    setSelectedDisk(disk);
  }, []);

  const handleModalClose = useCallback(() => {
    setSelectedDisk(null);
  }, []);

  return (
    <FieldSet legend={translate('DiskSpace')}>
      {isFetching ? <LoadingIndicator /> : null}

      {isFetching ? null : (
        <Table columns={columns}>
          <TableBody>
            {data.map((item) => {
              return (
                <DiskSpaceRow
                  key={item.path}
                  path={item.path}
                  label={item.label}
                  freeSpace={item.freeSpace}
                  totalSpace={item.totalSpace}
                  onRowPress={handleRowPress}
                />
              );
            })}
          </TableBody>
        </Table>
      )}

      {selectedDisk ? (
        <StorageUsageModal
          isOpen={true}
          diskPath={selectedDisk.path}
          diskLabel={selectedDisk.label}
          onModalClose={handleModalClose}
        />
      ) : null}
    </FieldSet>
  );
}

export default DiskSpace;
