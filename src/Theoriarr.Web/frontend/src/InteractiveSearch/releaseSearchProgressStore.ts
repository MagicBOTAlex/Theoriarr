import { create } from 'zustand';

export type ReleaseSearchIndexerStatus = 'searching' | 'done' | 'failed';

export interface ReleaseSearchIndexerProgress {
  id: number;
  name: string;
  status: ReleaseSearchIndexerStatus;
  releaseCount: number;
}

export interface ReleaseSearchProgress {
  indexers: ReleaseSearchIndexerProgress[];
  isComplete: boolean;
}

interface ReleaseSearchProgressState {
  searches: Record<string, ReleaseSearchProgress>;
  beginSearch: (searchId: string) => void;
  startSearch: (
    searchId: string,
    indexers: { id: number; name: string }[]
  ) => void;
  completeIndexer: (
    searchId: string,
    indexerId: number,
    indexerName: string,
    releaseCount: number,
    failed: boolean
  ) => void;
  completeSearch: (searchId: string) => void;
  clearSearch: (searchId: string) => void;
}

const useReleaseSearchProgressStore = create<ReleaseSearchProgressState>()(
  (set) => ({
    searches: {},

    beginSearch: (searchId) =>
      set((state) => {
        const existing = state.searches[searchId];

        // A completed search is reset so the same id can start a fresh search.
        if (existing && !existing.isComplete) {
          return state;
        }

        return {
          searches: {
            ...state.searches,
            [searchId]: { indexers: [], isComplete: false },
          },
        };
      }),

    startSearch: (searchId, indexers) =>
      set((state) => {
        const byId = new Map(
          (state.searches[searchId]?.indexers ?? []).map((indexer) => [
            indexer.id,
            indexer,
          ])
        );

        indexers.forEach((indexer) => {
          // A search can fan out over several criteria (scene mappings); keep the status
          // of indexers that have already reported in for an earlier fan-out.
          if (byId.has(indexer.id)) {
            return;
          }

          byId.set(indexer.id, {
            id: indexer.id,
            name: indexer.name,
            status: 'searching',
            releaseCount: 0,
          });
        });

        return {
          searches: {
            ...state.searches,
            [searchId]: {
              indexers: Array.from(byId.values()),
              isComplete: false,
            },
          },
        };
      }),

    completeIndexer: (searchId, indexerId, indexerName, releaseCount, failed) =>
      set((state) => {
        const search = state.searches[searchId];

        if (!search) {
          return state;
        }

        const indexers = search.indexers.map((indexer) => {
          if (indexer.id !== indexerId) {
            return indexer;
          }

          return {
            ...indexer,
            name: indexerName || indexer.name,
            status: failed ? ('failed' as const) : ('done' as const),
            releaseCount,
          };
        });

        return {
          searches: {
            ...state.searches,
            [searchId]: { ...search, indexers },
          },
        };
      }),

    completeSearch: (searchId) =>
      set((state) => {
        const search = state.searches[searchId];

        if (!search) {
          return state;
        }

        return {
          searches: {
            ...state.searches,
            [searchId]: { ...search, isComplete: true },
          },
        };
      }),

    clearSearch: (searchId) =>
      set((state) => {
        if (!state.searches[searchId]) {
          return state;
        }

        const searches = { ...state.searches };
        delete searches[searchId];

        return { searches };
      }),
  })
);

export const useReleaseSearchProgress = (searchId: string) => {
  return useReleaseSearchProgressStore(
    (state) => state.searches[searchId] ?? null
  );
};

export const getReleaseSearchProgressActions = () =>
  useReleaseSearchProgressStore.getState();
