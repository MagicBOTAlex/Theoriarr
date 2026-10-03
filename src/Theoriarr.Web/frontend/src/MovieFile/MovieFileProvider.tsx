import React, {
  createContext,
  PropsWithChildren,
  useContext,
  useMemo,
} from 'react';
import { MovieFile } from './MovieFile';
import useMovieFiles from './useMovieFiles';

export const MovieFileContext = createContext<
  ReadonlyArray<MovieFile> | undefined
>(undefined);

export default function MovieFileProvider({
  children,
  movieId,
}: PropsWithChildren<{ movieId: number }>) {
  const { data } = useMovieFiles({ movieId });

  return (
    <MovieFileContext.Provider value={data}>
      {children}
    </MovieFileContext.Provider>
  );
}

export function useMovieFile(id: number | undefined) {
  const movieFiles = useContext(MovieFileContext);

  return useMemo(() => {
    if (id === undefined) {
      return undefined;
    }

    return movieFiles?.find((item) => item.id === id);
  }, [id, movieFiles]);
}
