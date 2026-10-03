import React from 'react';
import Link from 'Components/Link/Link';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';

const PAGE_CLASS = 'p-[12px]';
const BUTTON_CLASS =
  'py-[6px] px-[12px] text-[13px] text-[var(--textColor)] hover:text-[var(--textColor)] cursor-pointer bg-[var(--buttonBackgroundColor,transparent)] border border-solid border-[var(--defaultBorderColor)] rounded-[3px] hover:bg-[var(--buttonHoverBackgroundColor,rgba(255,255,255,0.08))] disabled:cursor-default disabled:opacity-50';
const TABLE_CLASS =
  'w-full text-[13px] border-collapse [&_th]:p-[8px] [&_th]:text-left [&_th]:text-[var(--disabledColor)] [&_th]:border-b [&_th]:border-b-solid [&_th]:border-b-[color-mix(in_srgb,var(--color-base-200),transparent_50%)] [&_td]:p-[8px] [&_td]:align-top [&_td]:text-[var(--textColor)] [&_td]:border-b [&_td]:border-b-solid [&_td]:border-b-[color-mix(in_srgb,var(--color-base-200),transparent_50%)]';
const STATE_CLASS = 'p-[24px] text-center text-[var(--disabledColor)]';
const ERROR_STATE_CLASS = 'p-[24px] text-center text-[var(--dangerColor)]';
const MESSAGE_CLASS = 'mb-[12px] text-[13px] text-[var(--successColor)]';

interface UnmappedFolder {
  name: string;
  path: string;
  relativePath: string;
}

interface RootFolder {
  id: number;
  path: string;
  unmappedFolders?: UnmappedFolder[];
}

function MovieLibraryImportPage() {
  const { data, isLoading, error } = useApiQuery<RootFolder[]>({
    service: 'movies',
    path: '/rootfolder',
  });

  const rootFolders = data ?? [];

  const folders = rootFolders.flatMap((rootFolder) =>
    (rootFolder.unmappedFolders ?? []).map((folder) => ({
      ...folder,
      rootFolderPath: rootFolder.path,
    }))
  );

  return (
    <PageContentBody>
      <div className={PAGE_CLASS}>
        <p className={MESSAGE_CLASS}>
          {folders.length} unmapped folder
          {folders.length === 1 ? '' : 's'} found across {rootFolders.length}{' '}
          root folder{rootFolders.length === 1 ? '' : 's'}. Use Lookup to search
          for the matching movie and add it to the library.
        </p>

        {isLoading && !data ? (
          <div className={STATE_CLASS}>Scanning root folders...</div>
        ) : null}

        {error ? (
          <div className={ERROR_STATE_CLASS}>
            Failed to load root folders: {error.message}
          </div>
        ) : null}

        {!isLoading && !error && !folders.length ? (
          <div className={STATE_CLASS}>
            No unmapped folders found. Add a root folder in Movies settings and
            place movies on disk.
          </div>
        ) : null}

        {folders.length ? (
          <table className={TABLE_CLASS}>
            <thead>
              <tr>
                <th>Folder</th>
                <th>Root Folder</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {folders.map((folder) => (
                <tr key={folder.path}>
                  <td>{folder.relativePath || folder.name}</td>
                  <td>{folder.rootFolderPath}</td>
                  <td>
                    <Link
                      className={BUTTON_CLASS}
                      to={`/add/new?type=movies&term=${encodeURIComponent(
                        folder.name
                      )}&rootFolder=${encodeURIComponent(
                        folder.rootFolderPath
                      )}`}
                    >
                      Lookup
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : null}
      </div>
    </PageContentBody>
  );
}

export default MovieLibraryImportPage;
