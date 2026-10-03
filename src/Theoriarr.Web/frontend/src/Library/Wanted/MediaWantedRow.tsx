import React from 'react';
import Link from 'Components/Link/Link';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import EpisodeSearchCell from 'Episode/EpisodeSearchCell';
import { EpisodeEntity } from 'Episode/useEpisode';
import MovieSearchCell from 'Movies/MovieSearchCell';
import { MediaWantedItem } from './mediaWanted';

interface MediaWantedRowProps {
  item: MediaWantedItem;
  episodeEntity: EpisodeEntity;
}

function MediaWantedRow({ item, episodeEntity }: MediaWantedRowProps) {
  return (
    <TableRow>
      <TableRowCell>{item.type === 'series' ? 'Show' : 'Movie'}</TableRowCell>

      <TableRowCell>
        <Link to={item.link}>{item.title}</Link>
      </TableRowCell>

      <TableRowCell>{item.subtitle}</TableRowCell>

      <TableRowCell>
        {item.date ? <RelativeDateCell date={item.date} /> : null}
      </TableRowCell>

      <TableRowCell>{item.monitored ? 'Yes' : 'No'}</TableRowCell>

      <TableRowCell>{item.status}</TableRowCell>

      {item.episode ? (
        <EpisodeSearchCell
          episodeId={item.episode.id}
          episodeEntity={episodeEntity}
          seriesId={item.episode.seriesId}
          episodeTitle={item.episode.title}
          showOpenSeriesButton={true}
        />
      ) : null}

      {item.movie ? (
        <MovieSearchCell
          movieId={item.movie.id}
          movieTitle={item.movie.title}
        />
      ) : null}
    </TableRow>
  );
}

export default MediaWantedRow;
