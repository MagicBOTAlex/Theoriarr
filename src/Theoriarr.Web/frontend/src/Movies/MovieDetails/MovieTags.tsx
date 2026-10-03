import React from 'react';
import Label from 'Components/Label';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { kinds, sizes } from 'Helpers/Props';
import { useSingleMovie } from 'Movies/useMovies';
import { Tag } from 'Tags/useTags';
import sortByProp from 'Utilities/Array/sortByProp';

interface MovieTagsProps {
  movieId: number;
}

const DEFAULT_TAGS: Tag[] = [];

function MovieTags({ movieId }: MovieTagsProps) {
  const movie = useSingleMovie(movieId);
  const { data } = useApiQuery<Tag[]>({
    service: 'movies',
    path: '/tag',
    queryOptions: {
      gcTime: Infinity,
    },
  });

  const tagList = data ?? DEFAULT_TAGS;

  const tags = (movie?.tags ?? [])
    .map((tagId) => tagList.find((tag) => tag.id === tagId))
    .filter((tag) => !!tag)
    .sort(sortByProp('label'))
    .map((tag) => tag.label);

  return (
    <div>
      {tags.map((tag) => {
        return (
          <Label key={tag} kind={kinds.INFO} size={sizes.LARGE}>
            {tag}
          </Label>
        );
      })}
    </div>
  );
}

export default MovieTags;
