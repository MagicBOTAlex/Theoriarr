import React from 'react';
import { Tag, useTagList } from 'Tags/useTags';
import TagList from './TagList';

interface MovieTagListProps {
  movieId?: number;
  tags: number[];
  tagList?: ReadonlyArray<Tag>;
}

function MovieTagList({ tags, tagList }: MovieTagListProps) {
  const fetchedTagList = useTagList();

  return <TagList tags={tags} tagList={tagList ?? fetchedTagList} />;
}

export default MovieTagList;
