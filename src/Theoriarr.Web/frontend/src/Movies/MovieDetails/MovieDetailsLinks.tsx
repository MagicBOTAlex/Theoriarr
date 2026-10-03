import React, { useMemo } from 'react';
import Label, { LabelProps } from 'Components/Label';
import ClipboardButton from 'Components/Link/ClipboardButton';
import Link from 'Components/Link/Link';
import { kinds, sizes } from 'Helpers/Props';
import { Movie } from 'Movies/Movie';
import translate from 'Utilities/String/translate';

const LINKS_CLASS =
  'flex flex-wrap m-0 max-[480px]:flex-col max-[480px]:flex-wrap';
const LINK_CLASS = 'whitespace-nowrap';
const LINK_BLOCK_CLASS = 'flex m-[3px]';

type MovieDetailsLinksProps = Pick<
  Movie,
  'tmdbId' | 'imdbId' | 'youTubeTrailerId'
>;

interface MovieDetailsLink {
  externalId?: string | number;
  name: string;
  url: string;
  kind?: LabelProps['kind'];
}

function MovieDetailsLinks(props: MovieDetailsLinksProps) {
  const { tmdbId, imdbId, youTubeTrailerId } = props;

  const links = useMemo(() => {
    const validLinks: MovieDetailsLink[] = [];

    if (tmdbId) {
      validLinks.push(
        {
          externalId: tmdbId,
          name: 'TMDb',
          url: `https://www.themoviedb.org/movie/${tmdbId}`,
        },
        {
          name: 'Letterboxd',
          url: `https://letterboxd.com/tmdb/${tmdbId}`,
        }
      );
    }

    if (imdbId) {
      validLinks.push(
        {
          externalId: imdbId,
          name: 'IMDb',
          url: `https://imdb.com/title/${imdbId}/`,
        },
        {
          name: 'Trakt',
          url: `https://trakt.tv/movies/${imdbId}`,
        },
        {
          name: 'Movie Chat',
          url: `https://moviechat.org/${imdbId}/`,
        },
        {
          name: 'MDBList',
          url: `https://mdblist.com/movie/${imdbId}`,
        },
        {
          name: 'Blu-ray',
          url: `https://www.blu-ray.com/search/?quicksearch=1&quicksearch_keyword=${imdbId}&section=theatrical`,
        }
      );
    }

    if (youTubeTrailerId) {
      validLinks.push({
        name: translate('Trailer'),
        url: `https://www.youtube.com/watch?v=${youTubeTrailerId}`,
        kind: kinds.DANGER,
      });
    }

    return validLinks.sort(
      (a, b) => Number(!a.externalId) - Number(!b.externalId)
    );
  }, [tmdbId, imdbId, youTubeTrailerId]);

  return (
    <div className={LINKS_CLASS}>
      {links.map((link) => (
        <div key={link.name} className={LINK_BLOCK_CLASS}>
          <Link className={LINK_CLASS} to={link.url}>
            <Label
              className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default m-0! rounded-tr-none rounded-br-none hover:cursor-pointer"
              kind={link.kind ?? kinds.INFO}
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

export default MovieDetailsLinks;
