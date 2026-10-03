import translate from 'Utilities/String/translate';
import { TranscodeJob } from './useTranscodeJobs';

export interface JobSeasonGroup {
  key: string;
  label: string;
  sortValue: number;
  jobs: TranscodeJob[];
}

export interface JobSeriesGroup {
  key: string;
  title: string;
  seasons: JobSeasonGroup[];
}

// Movies and series never share an owner id, so the prefix keeps their keys distinct.
function ownerKey(job: TranscodeJob) {
  if (job.mediaType === 'Movie') {
    return job.movieId ? `movie-${job.movieId}` : 'movie-unknown';
  }

  return job.seriesId ? `series-${job.seriesId}` : 'series-unknown';
}

function ownerTitle(job: TranscodeJob) {
  if (job.mediaType === 'Movie') {
    return job.movieTitle ?? '';
  }

  return job.seriesTitle ?? '';
}

export function buildJobGroups(jobs: TranscodeJob[]): JobSeriesGroup[] {
  const groups: JobSeriesGroup[] = [];
  const byKey = new Map<string, JobSeriesGroup>();

  jobs.forEach((job) => {
    const key = ownerKey(job);
    let group = byKey.get(key);

    if (!group) {
      group = { key, title: ownerTitle(job), seasons: [] };
      byKey.set(key, group);
      groups.push(group);
    } else if (!group.title) {
      group.title = ownerTitle(job);
    }

    const sortValue = job.seasonNumber ?? -1;
    const seasonKey = `${key}-season-${sortValue}`;
    let season = group.seasons.find((item) => item.key === seasonKey);

    if (!season) {
      season = {
        key: seasonKey,
        label:
          typeof job.seasonNumber === 'number'
            ? translate('SeasonNumberToken', { seasonNumber: job.seasonNumber })
            : '',
        sortValue,
        jobs: [],
      };
      group.seasons.push(season);
    }

    season.jobs.push(job);
  });

  groups.forEach((group) => {
    group.seasons.sort((a, b) => a.sortValue - b.sortValue);

    if (!group.title) {
      group.title = translate('Unknown');
    }
  });

  groups.sort((a, b) => a.title.localeCompare(b.title));

  return groups;
}

export function flattenJobGroups(groups: JobSeriesGroup[]): TranscodeJob[] {
  return groups.flatMap((group) =>
    group.seasons.flatMap((season) => season.jobs)
  );
}
