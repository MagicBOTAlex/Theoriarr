import { useQueryClient } from '@tanstack/react-query';
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiMutation from 'Helpers/Hooks/useApiMutation';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import formatBytes from 'Utilities/Number/formatBytes';

const PAGE_CLASS = 'p-[12px]';
const TOOLBAR_CLASS = 'flex flex-wrap items-center gap-[8px] mb-[12px]';
const INPUT_CLASS =
  'flex-1 min-w-[240px] py-[6px] px-[8px] text-[13px] text-[var(--textColor)] bg-[var(--inputBackgroundColor,transparent)] border border-solid border-[var(--defaultBorderColor)] rounded-[3px]';
const BUTTON_CLASS =
  'py-[6px] px-[12px] text-[13px] text-[var(--textColor)] cursor-pointer bg-[var(--buttonBackgroundColor,transparent)] border border-solid border-[var(--defaultBorderColor)] rounded-[3px] hover:bg-[var(--buttonHoverBackgroundColor,rgba(255,255,255,0.08))] disabled:cursor-default disabled:opacity-50';
const PRIMARY_BUTTON_CLASS =
  'text-[var(--white)] bg-[var(--successColor)] border-[var(--successColor)]';
const TABLE_CLASS =
  'w-full text-[13px] border-collapse [&_th]:p-[8px] [&_th]:text-left [&_th]:text-[var(--disabledColor)] [&_th]:border-b [&_th]:border-b-solid [&_th]:border-b-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] [&_td]:p-[8px] [&_td]:align-top [&_td]:text-[var(--textColor)] [&_td]:border-b [&_td]:border-b-solid [&_td]:border-b-[color-mix(in_srgb,var(--color-base-200),transparent_50%)]';
const STATE_CLASS = 'p-[24px] text-center text-[var(--disabledColor)]';
const ERROR_STATE_CLASS = 'p-[24px] text-center text-[var(--dangerColor)]';
const MESSAGE_CLASS = 'mb-[12px] text-[13px] text-[var(--successColor)]';
const MESSAGE_ERROR_CLASS = 'text-[var(--dangerColor)]';
const REJECTIONS_CLASS = 'text-[var(--dangerColor)]';

interface ManualImportRejection {
  reason: string;
  type: string;
}

interface ManualImportItem {
  path: string;
  relativePath?: string;
  folderName?: string;
  name: string;
  size: number;
  movie?: {
    id?: number;
    title?: string;
    year?: number;
    tmdbId?: number;
  };
  quality?: {
    quality?: {
      name?: string;
    };
  };
  languages?: unknown[];
  releaseGroup?: string;
  indexerFlags?: number;
  downloadId?: string;
  rejections?: ManualImportRejection[];
}

interface RootFolderOption {
  path: string;
}

interface ManualImportCommandBody {
  name: string;
  files: Record<string, unknown>[];
  importMode: string;
}

function isImportable(item: ManualImportItem) {
  return !!item.movie?.id && !item.rejections?.length;
}

function MovieImportPage() {
  const queryClient = useQueryClient();
  const [folder, setFolder] = useState('');
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [message, setMessage] = useState('');

  const { data: rootFolders } = useApiQuery<RootFolderOption[]>({
    service: 'movies',
    path: '/rootfolder',
  });

  useEffect(() => {
    if (!folder && rootFolders?.length) {
      setFolder(rootFolders[0].path);
    }
  }, [folder, rootFolders]);

  const trimmedFolder = folder.trim();

  const { data, isFetching, error } = useApiQuery<ManualImportItem[]>({
    service: 'movies',
    path: '/manualimport',
    queryParams: {
      folder: trimmedFolder,
      filterExistingFiles: false,
    },
    queryOptions: {
      enabled: trimmedFolder.length > 0,
    },
  });

  const items = useMemo(() => data ?? [], [data]);

  useEffect(() => {
    setSelected(new Set(items.filter(isImportable).map((item) => item.path)));
  }, [items]);

  const { mutate: runImport, isPending: isImporting } = useApiMutation<
    unknown,
    ManualImportCommandBody
  >({
    service: 'movies',
    path: '/command',
    method: 'POST',
    mutationOptions: {
      onSuccess: () => {
        setMessage('Import started');
        queryClient.invalidateQueries({ queryKey: ['movies', '/movie'] });
        queryClient.invalidateQueries({ queryKey: ['movies', '/queue'] });
        queryClient.invalidateQueries({ queryKey: ['movies', '/history'] });
      },
      onError: (importError) => {
        setMessage(`Failed to start import: ${importError.message}`);
      },
    },
  });

  const importableItems = useMemo(() => items.filter(isImportable), [items]);

  const handleToggle = useCallback((path: string) => {
    setSelected((previous) => {
      const next = new Set(previous);

      if (next.has(path)) {
        next.delete(path);
      } else {
        next.add(path);
      }

      return next;
    });
  }, []);

  const handleSelectAll = useCallback(() => {
    setSelected((previous) =>
      previous.size === importableItems.length
        ? new Set()
        : new Set(importableItems.map((item) => item.path))
    );
  }, [importableItems]);

  const handleImportPress = useCallback(() => {
    const files = items
      .filter((item) => selected.has(item.path) && isImportable(item))
      .map((item) => ({
        path: item.path,
        folderName: item.folderName,
        movieId: item.movie?.id,
        quality: item.quality,
        languages: item.languages,
        releaseGroup: item.releaseGroup,
        indexerFlags: item.indexerFlags,
        downloadId: item.downloadId,
      }));

    if (!files.length) {
      setMessage('Select at least one matched file to import.');
      return;
    }

    setMessage('');
    runImport({ name: 'MovieManualImport', files, importMode: 'move' });
  }, [items, selected, runImport]);

  const allSelected =
    importableItems.length > 0 && selected.size === importableItems.length;

  return (
    <PageContent title="Manual Import">
      <PageContentBody>
        <div className={PAGE_CLASS}>
          <div className={TOOLBAR_CLASS}>
            <input
              className={INPUT_CLASS}
              type="text"
              value={folder}
              placeholder="Folder to scan"
              onChange={(event) => setFolder(event.target.value)}
            />

            <button
              className={BUTTON_CLASS}
              type="button"
              disabled={!importableItems.length}
              onClick={handleSelectAll}
            >
              {allSelected ? 'Select None' : 'Select All'}
            </button>

            <button
              className={`${BUTTON_CLASS} ${PRIMARY_BUTTON_CLASS}`}
              type="button"
              disabled={isImporting || !selected.size}
              onClick={handleImportPress}
            >
              {isImporting ? 'Importing...' : 'Import Selected'}
            </button>
          </div>

          {message ? (
            <div
              className={`${MESSAGE_CLASS} ${
                message.startsWith('Failed') || message.startsWith('Select')
                  ? MESSAGE_ERROR_CLASS
                  : ''
              }`}
            >
              {message}
            </div>
          ) : null}

          {isFetching && !items.length ? (
            <div className={STATE_CLASS}>Scanning folder...</div>
          ) : null}

          {error ? (
            <div className={ERROR_STATE_CLASS}>
              Failed to scan folder: {error.message}
            </div>
          ) : null}

          {!isFetching && !error && !items.length ? (
            <div className={STATE_CLASS}>
              No importable files found in this folder.
            </div>
          ) : null}

          {items.length ? (
            <table className={TABLE_CLASS}>
              <thead>
                <tr>
                  <th />
                  <th>Name</th>
                  <th>Movie</th>
                  <th>Quality</th>
                  <th>Size</th>
                  <th>Rejections</th>
                </tr>
              </thead>
              <tbody>
                {items.map((item) => {
                  const importable = isImportable(item);

                  return (
                    <tr key={item.path}>
                      <td>
                        <input
                          type="checkbox"
                          checked={selected.has(item.path)}
                          disabled={!importable}
                          onChange={() => handleToggle(item.path)}
                        />
                      </td>
                      <td>{item.relativePath ?? item.name}</td>
                      <td>
                        {item.movie?.title
                          ? `${item.movie.title}${
                              item.movie.year ? ` (${item.movie.year})` : ''
                            }`
                          : 'Unmatched'}
                      </td>
                      <td>{item.quality?.quality?.name ?? '-'}</td>
                      <td>{formatBytes(item.size)}</td>
                      <td className={REJECTIONS_CLASS}>
                        {item.rejections?.map((r) => r.reason).join(', ') ?? ''}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          ) : null}
        </div>
      </PageContentBody>
    </PageContent>
  );
}

export default MovieImportPage;
