import React, { useMemo } from 'react';
import Label from 'Components/Label';
import ClipboardButton from 'Components/Link/ClipboardButton';
import Link from 'Components/Link/Link';
import { kinds, sizes } from 'Helpers/Props';
import Series from 'Series/Series';
import translate from 'Utilities/String/translate';

type SeriesDetailsLinksProps = Pick<
  Series,
  'tvdbId' | 'tvMazeId' | 'imdbId' | 'tmdbId'
>;

interface SeriesDetailsLink {
  externalId?: string | number;
  name: string;
  url: string;
}

function SeriesDetailsLinks(props: SeriesDetailsLinksProps) {
  const { tvdbId, tvMazeId, imdbId, tmdbId } = props;

  const links = useMemo(() => {
    const validLinks: SeriesDetailsLink[] = [];

    if (tvdbId) {
      validLinks.push({
        externalId: tvdbId,
        name: 'The TVDB',
        url: `https://www.thetvdb.com/?tab=series&id=${tvdbId}`,
      });
    }

    if (tvMazeId) {
      validLinks.push({
        externalId: tvMazeId,
        name: 'TV Maze',
        url: `https://www.tvmaze.com/shows/${tvMazeId}/_`,
      });
    }

    if (imdbId) {
      validLinks.push(
        {
          externalId: imdbId,
          name: 'IMDB',
          url: `https://imdb.com/title/${imdbId}/`,
        },
        {
          name: 'Trakt',
          url: `https://trakt.tv/shows/${imdbId}`,
        },
        {
          name: 'MDBList',
          url: `https://mdblist.com/show/${imdbId}`,
        }
      );
    }

    if (tmdbId) {
      validLinks.push({
        externalId: tmdbId,
        name: 'TMDB',
        url: `https://www.themoviedb.org/tv/${tmdbId}`,
      });
    }

    return validLinks.sort(
      (a, b) => Number(!a.externalId) - Number(!b.externalId)
    );
  }, [tvdbId, tvMazeId, imdbId, tmdbId]);

  return (
    <div className="flex flex-wrap m-0 max-[480px]:flex-col max-[480px]:flex-wrap">
      {links.map((link) => (
        <div key={link.name} className="flex m-[3px]">
          <Link className="whitespace-nowrap" to={link.url}>
            <Label
              className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default m-0! rounded-tr-none rounded-br-none hover:cursor-pointer"
              kind={kinds.INFO}
              size={sizes.LARGE}
            >
              {link.name}
            </Label>
          </Link>

          {link.externalId ? (
            <ClipboardButton
              value={`${link.externalId}`}
              title={translate('CopyToClipboard')}
              kind={kinds.DEFAULT}
              size={sizes.SMALL}
              label={link.externalId}
            />
          ) : null}
        </div>
      ))}
    </div>
  );
}

export default SeriesDetailsLinks;
