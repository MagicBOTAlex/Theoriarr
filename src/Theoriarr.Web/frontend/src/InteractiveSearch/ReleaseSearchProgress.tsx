import classNames from 'classnames';
import React from 'react';
import Button from 'Components/Link/Button';
import { sizes } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import {
  ReleaseSearchIndexerProgress,
  useReleaseSearchProgress,
} from './releaseSearchProgressStore';

interface ReleaseSearchProgressProps {
  searchId: string;
  isRefreshing: boolean;
  onDismiss: () => void;
  onRefresh: () => void;
}

function getStatusText(indexer: ReleaseSearchIndexerProgress) {
  if (indexer.status === 'searching') {
    return translate('SearchingIndexer');
  }

  if (indexer.status === 'failed') {
    return translate('Failed');
  }

  return translate('ReleaseCount', { count: indexer.releaseCount });
}

function ReleaseSearchProgress({
  searchId,
  isRefreshing,
  onDismiss,
  onRefresh,
}: ReleaseSearchProgressProps) {
  const progress = useReleaseSearchProgress(searchId);

  if (!progress) {
    return null;
  }

  const { isComplete } = progress;

  const completedCount = progress.indexers.filter(
    (indexer) => indexer.status !== 'searching'
  ).length;

  return (
    <div className="mb-4 rounded-lg border border-[var(--borderColor)] bg-[color-mix(in_srgb,var(--textColor),transparent_97%)] p-4">
      <div className="mb-2 flex items-center justify-between gap-3">
        <span className="text-sm font-semibold">
          {isComplete
            ? translate('IndexerSearchComplete')
            : `${translate('SearchingIndexers')} (${completedCount}/${
                progress.indexers.length
              })`}
        </span>

        <div className="flex items-center gap-3">
          <Button
            size={sizes.SMALL}
            isDisabled={isRefreshing}
            onPress={onRefresh}
          >
            {translate('Refresh')}
          </Button>

          {isComplete ? null : (
            <Button size={sizes.SMALL} onPress={onDismiss}>
              {translate('Skip')}
            </Button>
          )}
        </div>
      </div>

      {isComplete ? (
        <div className="mb-2 text-xs">
          {translate('IndexerSearchCompleteHint')}
        </div>
      ) : null}

      {progress.indexers.length ? (
        <div className="flex flex-col gap-2">
          {progress.indexers.map((indexer) => {
            return (
              <div key={indexer.id} className="flex items-center gap-3">
                <span className="w-48 truncate text-xs">{indexer.name}</span>

                <div className="h-2 flex-1 overflow-hidden rounded-full bg-gray-500/20">
                  <div
                    className={classNames(
                      'h-full rounded-full transition-all duration-300',
                      indexer.status === 'searching'
                        ? 'w-1/3 animate-pulse bg-blue-500'
                        : null,
                      indexer.status === 'done' ? 'w-full bg-green-500' : null,
                      indexer.status === 'failed' ? 'w-full bg-red-500' : null
                    )}
                  />
                </div>

                <span className="w-24 text-right text-xs">
                  {getStatusText(indexer)}
                </span>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="text-xs">{translate('SearchingIndexer')}</div>
      )}
    </div>
  );
}

export default ReleaseSearchProgress;
