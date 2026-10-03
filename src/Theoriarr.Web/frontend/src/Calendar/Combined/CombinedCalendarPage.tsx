import React, { useCallback, useMemo, useState } from 'react';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { align, icons } from 'Helpers/Props';

const STATUS_DOWNLOADED_CLASS =
  'px-[6px] py-[2px] text-[11px] rounded-[3px] bg-[var(--successColor,#27ae60)] text-[var(--white,#fff)]';
const STATUS_MISSING_CLASS =
  'px-[6px] py-[2px] text-[11px] rounded-[3px] bg-[var(--dangerColor,#e74c3c)] text-[var(--white,#fff)]';
const BADGE_EPISODE_CLASS =
  'ml-auto px-[8px] py-[2px] text-[11px] font-bold uppercase rounded-[10px] bg-[var(--infoColor,#3498db)] text-[var(--white,#fff)]';
const BADGE_MOVIE_CLASS =
  'ml-auto px-[8px] py-[2px] text-[11px] font-bold uppercase rounded-[10px] bg-[var(--purple,#9b59b6)] text-[var(--white,#fff)]';

interface SeriesEpisode {
  id: number;
  title: string;
  airDateUtc: string;
  seasonNumber: number;
  episodeNumber: number;
  hasFile: boolean;
  seriesId: number;
  series?: {
    id: number;
    title: string;
    titleSlug: string;
  };
}

interface MovieRelease {
  id: number;
  title: string;
  year?: number;
  hasFile: boolean;
  inCinemas?: string;
  physicalRelease?: string;
  digitalRelease?: string;
  releaseDate?: string;
}

interface CalendarEntry {
  key: string;
  type: 'episode' | 'movie';
  title: string;
  meta: string;
  hasFile: boolean;
  link: string;
}

interface CalendarDay {
  key: string;
  date: Date;
  entries: CalendarEntry[];
}

const DAY_SPAN = 14;
const MAX_DAYS_WITH_ENTRIES = DAY_SPAN;

function startOfDay(date: Date) {
  const result = new Date(date);
  result.setHours(0, 0, 0, 0);

  return result;
}

function addDays(date: Date, days: number) {
  const result = new Date(date);
  result.setDate(result.getDate() + days);

  return result;
}

function toDateKey(date: Date) {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${date.getFullYear()}-${month}-${day}`;
}

function parseDate(value?: string) {
  if (!value) {
    return null;
  }

  const date = new Date(value);

  return isNaN(date.getTime()) ? null : startOfDay(date);
}

function CombinedCalendarPage() {
  const [start, setStart] = useState(() => startOfDay(new Date()));
  const [includeUnmonitored, setIncludeUnmonitored] = useState(true);

  const end = useMemo(() => addDays(start, DAY_SPAN - 1), [start]);
  const endExclusive = useMemo(() => addDays(end, 1), [end]);

  const episodesQuery = useApiQuery<SeriesEpisode[]>({
    service: 'series',
    path: '/calendar',
    queryParams: {
      start: start.toISOString(),
      end: endExclusive.toISOString(),
      includeUnmonitored,
      includeSpecials: true,
    },
  });

  const moviesQuery = useApiQuery<MovieRelease[]>({
    service: 'movies',
    path: '/calendar',
    queryParams: {
      start: start.toISOString(),
      end: endExclusive.toISOString(),
      unmonitored: includeUnmonitored,
    },
  });

  const days = useMemo(() => {
    const byDate = new Map<string, CalendarEntry[]>();

    const pushEntry = (entry: CalendarEntry, date: Date) => {
      const key = toDateKey(date);
      const entries = byDate.get(key) ?? [];

      entries.push({ ...entry, key: `${entry.key}-${key}` });
      byDate.set(key, entries);
    };

    (episodesQuery.data ?? []).forEach((episode) => {
      const date = parseDate(episode.airDateUtc);

      if (!date || date < start || date > end) {
        return;
      }

      pushEntry(
        {
          key: `ep-${episode.id}`,
          type: 'episode',
          title: episode.series?.title ?? episode.title,
          meta: `S${String(episode.seasonNumber).padStart(2, '0')}E${String(
            episode.episodeNumber
          ).padStart(2, '0')} \u00b7 ${episode.title}`,
          hasFile: episode.hasFile,
          link: episode.series?.titleSlug
            ? `/series/${episode.series.titleSlug}`
            : '/calendar',
        },
        date
      );
    });

    const candidates: { kind: string; field: keyof MovieRelease }[] = [
      { kind: 'Physical', field: 'physicalRelease' },
      { kind: 'Digital', field: 'digitalRelease' },
      { kind: 'Cinema', field: 'inCinemas' },
      { kind: 'Release', field: 'releaseDate' },
    ];

    (moviesQuery.data ?? []).forEach((movie) => {
      const seen = new Set<string>();

      candidates.forEach(({ kind, field }) => {
        const date = parseDate(movie[field] as string | undefined);

        if (!date || date < start || date > end) {
          return;
        }

        const key = toDateKey(date);

        if (seen.has(key)) {
          return;
        }

        seen.add(key);

        pushEntry(
          {
            key: `mv-${movie.id}-${kind}`,
            type: 'movie',
            title: movie.title,
            meta: `${kind} release`,
            hasFile: movie.hasFile,
            link: `/movie/${movie.id}`,
          },
          date
        );
      });
    });

    const result: CalendarDay[] = [];

    for (let index = 0; index < MAX_DAYS_WITH_ENTRIES; index += 1) {
      const date = addDays(start, index);
      const entries = byDate.get(toDateKey(date)) ?? [];

      if (entries.length) {
        entries.sort((a, b) => a.title.localeCompare(b.title));
        result.push({ key: toDateKey(date), date, entries });
      }
    }

    return result;
  }, [episodesQuery.data, moviesQuery.data, start, end]);

  const handlePreviousPress = useCallback(() => {
    setStart((previous) => addDays(previous, -DAY_SPAN));
  }, []);

  const handleNextPress = useCallback(() => {
    setStart((previous) => addDays(previous, DAY_SPAN));
  }, []);

  const handleTodayPress = useCallback(() => {
    setStart(startOfDay(new Date()));
  }, []);

  const handleUnmonitoredChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      setIncludeUnmonitored(event.target.checked);
    },
    []
  );

  const dayFormatter = useMemo(
    () =>
      new Intl.DateTimeFormat(undefined, {
        weekday: 'long',
        month: 'short',
        day: 'numeric',
      }),
    []
  );

  const isLoading = episodesQuery.isLoading || moviesQuery.isLoading;
  const error = episodesQuery.error ?? moviesQuery.error;

  return (
    <PageContent title="Calendar">
      <PageToolbar>
        <PageToolbarSection>
          <PageToolbarButton
            label="Previous"
            iconName={icons.PAGE_PREVIOUS}
            onPress={handlePreviousPress}
          />

          <PageToolbarButton
            label="Today"
            iconName={icons.CALENDAR}
            onPress={handleTodayPress}
          />

          <PageToolbarButton
            label="Next"
            iconName={icons.PAGE_NEXT}
            onPress={handleNextPress}
          />
        </PageToolbarSection>

        <PageToolbarSection alignContent={align.RIGHT}>
          <label className="flex items-center gap-[6px] px-[10px] text-[13px] cursor-pointer">
            <input
              type="checkbox"
              checked={includeUnmonitored}
              onChange={handleUnmonitoredChange}
            />
            Include Unmonitored
          </label>
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody>
        {isLoading && !days.length ? (
          <div className="p-[24px] text-center">Loading calendar...</div>
        ) : null}

        {error ? (
          <div className="p-[24px] text-center text-[var(--dangerColor,#e74c3c)]">
            Failed to load calendar: {error.message}
          </div>
        ) : null}

        {!isLoading && !error && !days.length ? (
          <div className="p-[24px] text-center">
            No movies or episodes in this range.
          </div>
        ) : null}

        {days.length ? (
          <div className="flex flex-col gap-[12px] p-[12px]">
            {days.map((day) => (
              <div
                key={day.key}
                className="border border-solid border-[var(--borderColor,#4a4a4a)] rounded-[6px] overflow-hidden"
              >
                <div className="flex items-baseline gap-[8px] px-[12px] py-[8px] bg-[var(--tableHeaderBackgroundColor,rgba(255,255,255,0.05))]">
                  <span className="text-[12px] font-bold tracking-[0.04em] uppercase">
                    {dayFormatter.format(day.date)}
                  </span>
                  <span className="text-[12px] opacity-75">
                    {day.entries.length} item
                    {day.entries.length === 1 ? '' : 's'}
                  </span>
                </div>

                <div className="flex flex-col">
                  {day.entries.map((entry) => (
                    <Link
                      key={entry.key}
                      className="flex items-center gap-[12px] px-[12px] py-[8px] text-[inherit] no-underline border-t border-solid border-[var(--borderColor,#4a4a4a)] hover:bg-[var(--tableRowHoverBackgroundColor,rgba(255,255,255,0.05))]"
                      to={entry.link}
                    >
                      <div className="flex flex-col min-w-0">
                        <span className="overflow-hidden font-semibold text-ellipsis whitespace-nowrap">
                          {entry.title}
                        </span>
                        <span className="text-[12px] opacity-75">
                          {entry.meta}
                        </span>
                      </div>

                      <span
                        className={
                          entry.hasFile
                            ? STATUS_DOWNLOADED_CLASS
                            : STATUS_MISSING_CLASS
                        }
                      >
                        {entry.hasFile ? 'Downloaded' : 'Missing'}
                      </span>

                      <span
                        className={
                          entry.type === 'episode'
                            ? BADGE_EPISODE_CLASS
                            : BADGE_MOVIE_CLASS
                        }
                      >
                        {entry.type === 'episode' ? 'Episode' : 'Movie'}
                      </span>
                    </Link>
                  ))}
                </div>
              </div>
            ))}
          </div>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default CombinedCalendarPage;
