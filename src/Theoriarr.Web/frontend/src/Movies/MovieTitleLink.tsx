import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';

export interface MovieTitleLinkProps extends LinkProps {
  movieId: number;
  title: string;
  year?: number;
}

export default function MovieTitleLink({
  movieId,
  title,
  year = 0,
  ...linkProps
}: MovieTitleLinkProps) {
  const link = `/movie/${movieId}`;

  return (
    <Link to={link} title={title} {...linkProps}>
      {title}
      {year > 0 ? ` (${year})` : ''}
    </Link>
  );
}
