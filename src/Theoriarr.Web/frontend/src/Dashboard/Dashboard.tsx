import React from 'react';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import formatBytes from 'Utilities/Number/formatBytes';

interface MovieResource {
  id: number;
  title: string;
  monitored: boolean;
  hasFile: boolean;
  sizeOnDisk: number;
}

interface SeriesResource {
  id: number;
  title: string;
  monitored: boolean;
  seriesType: 'anime' | 'daily' | 'standard';
  statistics?: {
    episodeFileCount: number;
    totalEpisodeCount: number;
    sizeOnDisk: number;
  };
}

interface QueueStatusResource {
  totalCount: number;
  count: number;
  unknownCount: number;
  errors: boolean;
  warnings: boolean;
}

interface HealthResource {
  type: string;
  message: string;
}

interface DiskSpaceResource {
  path: string;
  label: string;
  freeSpace: number;
  totalSpace: number;
}

interface StatProps {
  value: React.ReactNode;
  label: string;
  isError?: boolean;
}

function Stat({ value, label, isError }: StatProps) {
  return (
    <div className="flex flex-col items-center justify-center bg-[var(--cardBackgroundColor)] px-2 py-4">
      <span
        className={
          isError
            ? 'text-[22px] font-semibold text-[var(--dangerColor)]'
            : 'text-[22px] font-semibold text-[var(--textColor)]'
        }
      >
        {value}
      </span>
      <span className="mt-1 text-[11px] uppercase tracking-[0.5px] text-[var(--disabledColor)]">
        {label}
      </span>
    </div>
  );
}

function Dashboard() {
  const movies = useApiQuery<MovieResource[]>({
    service: 'movies',
    path: '/movie',
  });

  const series = useApiQuery<SeriesResource[]>({
    service: 'series',
    path: '/series',
  });

  const movieQueue = useApiQuery<QueueStatusResource>({
    service: 'movies',
    path: '/queue/status',
  });

  const seriesQueue = useApiQuery<QueueStatusResource>({
    service: 'series',
    path: '/queue/status',
  });

  const movieHealth = useApiQuery<HealthResource[]>({
    service: 'movies',
    path: '/health',
  });

  const seriesHealth = useApiQuery<HealthResource[]>({
    service: 'series',
    path: '/health',
  });

  const movieDisk = useApiQuery<DiskSpaceResource[]>({
    service: 'movies',
    path: '/diskspace',
  });

  const seriesDisk = useApiQuery<DiskSpaceResource[]>({
    service: 'series',
    path: '/diskspace',
  });

  const movieList = movies.data ?? [];
  const seriesList = series.data ?? [];

  const animeCount = seriesList.filter(
    (item) => item.seriesType === 'anime'
  ).length;
  const showCount = seriesList.length - animeCount;

  const movieMissing = movieList.filter(
    (movie) => movie.monitored && !movie.hasFile
  ).length;

  const seriesMissing = seriesList.filter((item) => {
    const stats = item.statistics;

    return (
      item.monitored &&
      stats != null &&
      stats.episodeFileCount < stats.totalEpisodeCount
    );
  }).length;

  const sizeOnDisk =
    movieList.reduce((acc, movie) => acc + (movie.sizeOnDisk ?? 0), 0) +
    seriesList.reduce(
      (acc, item) => acc + (item.statistics?.sizeOnDisk ?? 0),
      0
    );

  const queueCount =
    (movieQueue.data?.totalCount ?? 0) + (seriesQueue.data?.totalCount ?? 0);

  // The domains usually share the same disks, so de-duplicate by path before
  // totalling the free space rather than counting each disk twice.
  const disks = new Map<string, DiskSpaceResource>();

  [...(movieDisk.data ?? []), ...(seriesDisk.data ?? [])].forEach((disk) => {
    if (!disks.has(disk.path)) {
      disks.set(disk.path, disk);
    }
  });

  const freeSpace = Array.from(disks.values()).reduce(
    (acc, disk) => acc + disk.freeSpace,
    0
  );

  const health = new Map<string, HealthResource>();

  [...(seriesHealth.data ?? []), ...(movieHealth.data ?? [])].forEach(
    (item) => {
      health.set(`${item.type}:${item.message}`, item);
    }
  );

  const healthItems = Array.from(health.values());
  const healthErrors = healthItems.filter((item) => item.type === 'error');

  const isLoading =
    movies.isLoading &&
    series.isLoading &&
    movieList.length === 0 &&
    seriesList.length === 0;

  return (
    <PageContent title="Dashboard">
      <PageContentBody>
        {isLoading ? (
          <div className="p-5 text-[var(--disabledColor)]">
            Loading dashboard...
          </div>
        ) : (
          <div className="grid grid-cols-[repeat(auto-fit,minmax(340px,1fr))] gap-5 p-5">
            <div className="flex flex-col overflow-hidden rounded border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)] shadow-[0_1px_3px_var(--cardShadowColor)]">
              <div className="flex items-center justify-between border-b border-[var(--defaultBorderColor)] bg-[var(--pageHeaderBackgroundColor)] px-[18px] py-[14px]">
                <span className="text-[18px] font-semibold text-[var(--textColor)]">
                  Theoriarr
                </span>
                <span className="text-[12px] text-[var(--disabledColor)]">
                  {`Series ${window.Theoriarr.services.series.version} \u00b7 Movies ${window.Theoriarr.services.movies.version}`}
                </span>
              </div>

              <div className="grid grid-cols-3 gap-px bg-[var(--defaultBorderColor)]">
                <Stat value={showCount} label="Shows" />
                <Stat value={movieList.length} label="Movies" />
                <Stat value={animeCount} label="Anime" />
                <Stat value={movieMissing + seriesMissing} label="Missing" />
                <Stat value={queueCount} label="Queue" />
                <Stat value={formatBytes(sizeOnDisk)} label="On Disk" />
                <Stat
                  value={disks.size ? formatBytes(freeSpace) : '-'}
                  label="Free"
                />
                <Stat
                  value={healthErrors.length}
                  label="Health"
                  isError={healthErrors.length > 0}
                />
              </div>

              {healthErrors.length ? (
                <div className="flex items-center gap-2 border-t border-[var(--defaultBorderColor)] px-[18px] py-[10px] text-[12px] text-[var(--textColor)]">
                  <span className="text-[var(--dangerColor)]">
                    {healthErrors[0].message}
                  </span>
                </div>
              ) : null}

              <div className="flex flex-wrap gap-2 border-t border-[var(--defaultBorderColor)] px-[18px] py-3">
                <Link
                  className="rounded-[3px] border border-[var(--defaultBorderColor)] px-[10px] py-1 text-[12px] text-[var(--linkColor)] hover:border-[var(--themeBlue)] hover:text-[var(--linkHoverColor)]"
                  to="/series"
                >
                  Shows
                </Link>
                <Link
                  className="rounded-[3px] border border-[var(--defaultBorderColor)] px-[10px] py-1 text-[12px] text-[var(--linkColor)] hover:border-[var(--themeBlue)] hover:text-[var(--linkHoverColor)]"
                  to="/movie"
                >
                  Movies
                </Link>
                <Link
                  className="rounded-[3px] border border-[var(--defaultBorderColor)] px-[10px] py-1 text-[12px] text-[var(--linkColor)] hover:border-[var(--themeBlue)] hover:text-[var(--linkHoverColor)]"
                  to="/series"
                >
                  Anime
                </Link>
                <Link
                  className="rounded-[3px] border border-[var(--defaultBorderColor)] px-[10px] py-1 text-[12px] text-[var(--linkColor)] hover:border-[var(--themeBlue)] hover:text-[var(--linkHoverColor)]"
                  to="/wanted/missing"
                >
                  Wanted
                </Link>
                <Link
                  className="rounded-[3px] border border-[var(--defaultBorderColor)] px-[10px] py-1 text-[12px] text-[var(--linkColor)] hover:border-[var(--themeBlue)] hover:text-[var(--linkHoverColor)]"
                  to="/activity/queue"
                >
                  Queue
                </Link>
                <Link
                  className="rounded-[3px] border border-[var(--defaultBorderColor)] px-[10px] py-1 text-[12px] text-[var(--linkColor)] hover:border-[var(--themeBlue)] hover:text-[var(--linkHoverColor)]"
                  to="/settings"
                >
                  Settings
                </Link>
              </div>
            </div>
          </div>
        )}
      </PageContentBody>
    </PageContent>
  );
}

export default Dashboard;
