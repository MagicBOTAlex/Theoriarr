import React, { useCallback, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import { icons, kinds, sizes, sortDirections } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { MediaItem } from 'Library/MediaItem';
import useLibraryItems from 'Library/useLibraryItems';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import StorageUsageBar from './StorageUsageBar';
import useDiskSpaceContent from './useDiskSpaceContent';

const COLUMNS: Column[] = [
  {
    name: 'title',
    label: () => translate('Name'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'type',
    label: () => translate('Type'),
    isSortable: true,
    isVisible: true,
    className: 'w-[100px]',
  },
  {
    name: 'sizeOnDisk',
    label: () => translate('SizeOnDisk'),
    isSortable: true,
    isVisible: true,
    className: 'w-[150px]',
  },
];

const OTHER_COLUMNS: Column[] = [
  {
    name: 'name',
    label: () => translate('Name'),
    isVisible: true,
  },
  {
    name: 'type',
    label: () => translate('Type'),
    isVisible: true,
    className: 'w-[100px]',
  },
  {
    name: 'size',
    label: () => translate('Size'),
    isVisible: true,
    className: 'w-[150px]',
  },
];

function normalizePath(path: string) {
  return path.replace(/\\/g, '/').replace(/\/+$/, '').toLowerCase();
}

function isOnDisk(itemPath: string, diskPath: string) {
  const disk = normalizePath(diskPath);
  const item = normalizePath(itemPath);

  if (!disk) {
    return true;
  }

  return item === disk || item.startsWith(`${disk}/`);
}

function getItemPath(item: MediaItem) {
  return item.type === 'movie' ? item.movie?.path : item.series?.path;
}

interface StorageUsageModalProps {
  isOpen: boolean;
  diskPath?: string;
  diskLabel?: string;
  onModalClose: () => void;
}

function StorageUsageModal({
  isOpen,
  diskPath,
  diskLabel,
  onModalClose,
}: StorageUsageModalProps) {
  const { items, isLoading, error } = useLibraryItems();
  const {
    data: otherContent,
    isLoading: isOtherLoading,
    error: otherError,
  } = useDiskSpaceContent(diskPath);
  const [sortKey, setSortKey] = useState('sizeOnDisk');
  const [sortDirection, setSortDirection] = useState<SortDirection>(
    sortDirections.DESCENDING
  );

  const onSortPress = useCallback(
    (value: string, direction?: SortDirection) => {
      if (value === sortKey) {
        setSortDirection((current) =>
          current === sortDirections.ASCENDING
            ? sortDirections.DESCENDING
            : sortDirections.ASCENDING
        );

        return;
      }

      setSortKey(value);
      setSortDirection(
        direction ??
          (value === 'sizeOnDisk'
            ? sortDirections.DESCENDING
            : sortDirections.ASCENDING)
      );
    },
    [sortKey]
  );

  const data = useMemo(() => {
    const filtered = diskPath
      ? items.filter((item) => {
          const path = getItemPath(item);

          return path ? isOnDisk(path, diskPath) : false;
        })
      : items;

    return filtered
      .filter((item) => item.sizeOnDisk > 0)
      .sort((a, b) => {
        const multiplier = sortDirection === sortDirections.ASCENDING ? 1 : -1;

        if (sortKey === 'title') {
          return a.title.localeCompare(b.title) * multiplier;
        }

        if (sortKey === 'type') {
          return a.type.localeCompare(b.type) * multiplier;
        }

        return (a.sizeOnDisk - b.sizeOnDisk) * multiplier;
      });
  }, [items, diskPath, sortKey, sortDirection]);

  const totalSize = useMemo(
    () => data.reduce((sum, item) => sum + item.sizeOnDisk, 0),
    [data]
  );

  const barItems = useMemo(
    () => data.map((item) => ({ label: item.title, value: item.sizeOnDisk })),
    [data]
  );

  return (
    <Modal isOpen={isOpen} size={sizes.LARGE} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{translate('StorageUsage')}</ModalHeader>

        <ModalBody>
          <div className="mb-[15px] flex justify-between gap-4 text-[13px] opacity-70">
            <span className="break-all">
              {diskPath}
              {diskLabel ? ` (${diskLabel})` : ''}
            </span>

            <span className="whitespace-nowrap">
              {translate('Total')}: {formatBytes(totalSize)}
            </span>
          </div>

          {isLoading ? <LoadingIndicator /> : null}

          {error ? <Alert kind={kinds.DANGER}>{error.message}</Alert> : null}

          {!isLoading && !error && !data.length ? (
            <Alert kind={kinds.INFO}>{translate('NoMediaOnDisk')}</Alert>
          ) : null}

          {!isLoading && !error && data.length ? (
            <>
              <div className="mb-[20px]">
                <StorageUsageBar items={barItems} />
              </div>

              <Table
                columns={COLUMNS}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={onSortPress}
              >
                <TableBody>
                  {data.map((item) => {
                    return (
                      <TableRow key={item.selectKey}>
                        <TableRowCell>
                          <Link to={item.link}>{item.title}</Link>
                        </TableRowCell>

                        <TableRowCell>
                          {item.type === 'movie'
                            ? translate('Movie')
                            : translate('Series')}
                        </TableRowCell>

                        <TableRowCell>
                          {formatBytes(item.sizeOnDisk)}
                        </TableRowCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </>
          ) : null}

          {isOtherLoading ? <LoadingIndicator /> : null}

          {otherError ? (
            <Alert kind={kinds.DANGER}>{otherError.message}</Alert>
          ) : null}

          {!isOtherLoading && !otherError && otherContent.length ? (
            <div className="mt-[30px]">
              <div className="mb-[10px] text-[18px] font-light">
                {translate('OtherFilesAndFolders')}
              </div>

              <Table columns={OTHER_COLUMNS}>
                <TableBody>
                  {otherContent.map((entry) => {
                    return (
                      <TableRow key={entry.path}>
                        <TableRowCell>
                          <Icon
                            name={entry.isFile ? icons.FILE : icons.FOLDER}
                            className="mr-[8px]"
                          />
                          {entry.name}
                        </TableRowCell>

                        <TableRowCell>
                          {entry.isFile
                            ? translate('File')
                            : translate('Folder')}
                        </TableRowCell>

                        <TableRowCell>{formatBytes(entry.size)}</TableRowCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          ) : null}
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Close')}</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default StorageUsageModal;
