import React, {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { hideMessage, showMessage } from 'App/messagesStore';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import ProgressBar from 'Components/ProgressBar';
import { kinds, sizes } from 'Helpers/Props';
import { ERROR, INFO, SUCCESS, WARNING } from 'Helpers/Props/messageTypes';
import {
  getDeviceLabel,
  useMediaCompressionCapabilities,
} from 'Settings/Compression/useMediaCompressionCapabilities';
import { useTranscodeProfiles } from 'Settings/Compression/useTranscodeProfiles';
import translate from 'Utilities/String/translate';
import getToggledRange from 'Utilities/Table/getToggledRange';
import { buildJobGroups, flattenJobGroups, JobSeriesGroup } from './jobGroups';
import TranscodeHistoryRow from './TranscodeHistoryRow';
import TranscodeJobRow from './TranscodeJobRow';
import {
  useBulkResolveTranscodeJobs,
  useClearTranscodeHistory,
  useCreateTranscodeJobs,
  useTranscodeJobs,
} from './useTranscodeJobs';

const EMPTY_CLASS = 'text-[var(--helpTextColor)]';
const GROUP_CLASS = 'mt-[10px] mb-[2px] text-[13px] font-semibold first:mt-0';
const SEASON_CLASS =
  'mt-[4px] mb-[2px] text-[12px] font-normal text-[var(--helpTextColor)]';
const BULK_MESSAGE_ID = -1000;
const REQUEUE_MESSAGE_ID = -1014;
const CLEAR_HISTORY_MESSAGE_ID = -1015;
const BULK_DETAIL_MESSAGE_ID = -1016;

// A bulk resolve is only finished once its jobs reach a terminal status (the copies can run for a
// long time after the batch is claimed), so progress is measured here rather than by review
// membership.
const TERMINAL_STATUSES = ['Completed', 'Failed', 'Cancelled', 'Skipped'];

function MediaCompressionPage() {
  const { isFetching, isFetched, error, jobs, refetch } = useTranscodeJobs();
  const { capabilities } = useMediaCompressionCapabilities();
  const { profiles } = useTranscodeProfiles();
  const { bulkResolveJobs } = useBulkResolveTranscodeJobs();
  const { clearHistory, isClearingHistory } = useClearTranscodeHistory();
  const { createJobs, isCreating } = useCreateTranscodeJobs();

  const handleRetry = useCallback(() => {
    refetch();
  }, [refetch]);

  const active = useMemo(
    () =>
      jobs
        .filter((job) => job.status === 'Queued' || job.status === 'Running')
        .sort((a, b) => a.id - b.id),
    [jobs]
  );

  const transferring = useMemo(
    () =>
      jobs
        .filter((job) => job.status === 'Transferring')
        .sort((a, b) => a.id - b.id),
    [jobs]
  );

  const reviewGroups = useMemo(
    () => buildJobGroups(jobs.filter((job) => job.status === 'AwaitingReview')),
    [jobs]
  );

  // Flattened in the same order the groups render so shift-select ranges stay contiguous.
  const review = useMemo(() => flattenJobGroups(reviewGroups), [reviewGroups]);

  const activeGroups = useMemo(() => buildJobGroups(active), [active]);
  const transferringGroups = useMemo(
    () => buildJobGroups(transferring),
    [transferring]
  );

  const history = useMemo(
    () =>
      jobs.filter(
        (job) =>
          job.status !== 'Queued' &&
          job.status !== 'Running' &&
          job.status !== 'Transferring' &&
          job.status !== 'AwaitingReview'
      ),
    [jobs]
  );

  const historyGroups = useMemo(() => buildJobGroups(history), [history]);

  const requeueJobIds = useMemo(
    () =>
      jobs
        .filter(
          (job) =>
            job.status === 'Failed' &&
            job.requeuedJobId == null &&
            (job.episodeFileId != null || job.movieFileId != null)
        )
        .map((job) => job.id),
    [jobs]
  );

  const requeueCount = requeueJobIds.length;

  const [selectedIds, setSelectedIds] = useState<number[]>([]);
  const [bulk, setBulk] = useState<{ ids: number[]; total: number } | null>(
    null
  );
  const [isClearHistoryModalOpen, setIsClearHistoryModalOpen] = useState(false);
  const [pendingBulkAction, setPendingBulkAction] = useState<string | null>(
    null
  );
  const lastSelected = useRef<number | null>(null);
  const selectAllRef = useRef<HTMLInputElement>(null);

  const deviceNames = useMemo(() => {
    const names: Record<string, string> = {};

    (capabilities.devices ?? []).forEach((device) => {
      names[device.deviceId] = getDeviceLabel(device);
    });

    return names;
  }, [capabilities.devices]);

  const profileNames = useMemo(() => {
    const names: Record<number, string> = {};

    profiles.forEach((profile) => {
      names[profile.id] = profile.name;
    });

    return names;
  }, [profiles]);

  const selectedSet = useMemo(() => new Set(selectedIds), [selectedIds]);

  const reviewKey = review.map((job) => job.id).join(',');
  const allSelected =
    review.length > 0 && review.every((job) => selectedSet.has(job.id));
  const someSelected = selectedIds.length > 0 && !allSelected;

  const terminalIds = useMemo(
    () =>
      new Set(
        jobs
          .filter((job) => TERMINAL_STATUSES.includes(job.status))
          .map((job) => job.id)
      ),
    [jobs]
  );

  const bulkDone = bulk
    ? bulk.ids.filter((id) => terminalIds.has(id)).length
    : 0;
  const bulkProgress = bulk && bulk.total ? (bulkDone * 100) / bulk.total : 0;

  const getDeviceName = useCallback(
    (deviceId?: string) => (deviceId ? deviceNames[deviceId] : undefined),
    [deviceNames]
  );

  const getProfileName = useCallback(
    (profileId?: number) => (profileId ? profileNames[profileId] : undefined),
    [profileNames]
  );

  const renderJobGroups = useCallback(
    (groups: JobSeriesGroup[]) => (
      <>
        {groups.map((group) => (
          <React.Fragment key={group.key}>
            <h3 className={GROUP_CLASS}>{group.title}</h3>

            {group.seasons.map((season) => (
              <React.Fragment key={season.key}>
                {season.label ? (
                  <h4 className={SEASON_CLASS}>{season.label}</h4>
                ) : null}

                {season.jobs.map((job) => (
                  <TranscodeJobRow
                    key={job.id}
                    job={job}
                    deviceName={getDeviceName(job.deviceId)}
                    profileName={getProfileName(job.profileId)}
                  />
                ))}
              </React.Fragment>
            ))}
          </React.Fragment>
        ))}
      </>
    ),
    [getDeviceName, getProfileName]
  );

  useEffect(() => {
    const ids = new Set(reviewKey ? reviewKey.split(',').map(Number) : []);

    setSelectedIds((current) => current.filter((id) => ids.has(id)));

    if (lastSelected.current != null && !ids.has(lastSelected.current)) {
      lastSelected.current = null;
    }
  }, [reviewKey]);

  useEffect(() => {
    if (selectAllRef.current) {
      selectAllRef.current.indeterminate = someSelected;
    }
  }, [someSelected]);

  useEffect(() => {
    if (!bulk) {
      return;
    }

    showMessage({
      id: BULK_MESSAGE_ID,
      name: translate('Transcoding'),
      message: translate('BulkResolveProgress', {
        done: Math.min(bulkDone, bulk.total),
        total: bulk.total,
      }),
      type: INFO,
      hideAfter: 0,
    });
  }, [bulk, bulkDone]);

  // Navigating away before the request settles would otherwise leave the persistent
  // (hideAfter: 0) progress message in the sidebar forever.
  useEffect(() => {
    return () => hideMessage({ id: BULK_MESSAGE_ID });
  }, []);

  const handleSelect = useCallback(
    (jobId: number, isSelected: boolean, shiftKey: boolean) => {
      // Capture the anchor before scheduling the update. The updater below runs later (React
      // invokes it while re-rendering), so reading `lastSelected.current` inside it would see the
      // id we just clicked and collapse the shift range to a single row.
      const anchor = lastSelected.current;
      lastSelected.current = jobId;

      setSelectedIds((current) => {
        const next = new Set(current);

        if (shiftKey && anchor != null) {
          const { lower, upper } = getToggledRange(review, jobId, anchor);
          const start = Math.max(0, lower);
          const end = Math.min(upper, review.length);

          for (let index = start; index < end; index++) {
            if (isSelected) {
              next.add(review[index].id);
            } else {
              next.delete(review[index].id);
            }
          }
        }

        if (isSelected) {
          next.add(jobId);
        } else {
          next.delete(jobId);
        }

        return Array.from(next);
      });
    },
    [review]
  );

  const handleSelectAll = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      lastSelected.current = null;
      setSelectedIds(event.target.checked ? review.map((job) => job.id) : []);
    },
    [review]
  );

  const startBulkResolve = useCallback(
    (action: string) => {
      const ids = [...selectedIds];

      if (!ids.length) {
        return;
      }

      lastSelected.current = null;
      setSelectedIds([]);
      setBulk({ ids, total: ids.length });

      showMessage({
        id: BULK_MESSAGE_ID,
        name: translate('Transcoding'),
        message: translate('BulkResolveStarted', { count: ids.length }),
        type: INFO,
        hideAfter: 0,
      });

      bulkResolveJobs(
        { action, jobIds: ids },
        {
          onSuccess: (result) => {
            setBulk(null);

            const resolvedCount = result?.resolved?.length ?? 0;
            const failures = result?.failed ?? [];

            if (failures.length > 0) {
              showMessage({
                id: BULK_MESSAGE_ID,
                name: translate('Transcoding'),
                message: translate('BulkResolvePartial', {
                  resolved: resolvedCount,
                  total: ids.length,
                  failed: failures.length,
                }),
                type: WARNING,
                hideAfter: 10,
              });

              // Surface the first per-id reason (V1): the response now carries it.
              showMessage({
                id: BULK_DETAIL_MESSAGE_ID,
                name: translate('Transcoding'),
                message: failures[0].error,
                type: ERROR,
                hideAfter: 15,
              });

              return;
            }

            showMessage({
              id: BULK_MESSAGE_ID,
              name: translate('Transcoding'),
              message: translate('BulkResolveComplete', { count: ids.length }),
              type: SUCCESS,
              hideAfter: 4,
            });
          },
          onError: () => {
            setBulk(null);
            showMessage({
              id: BULK_MESSAGE_ID,
              name: translate('Transcoding'),
              message: translate('BulkResolveFailed'),
              type: ERROR,
              hideAfter: 10,
            });
          },
        }
      );
    },
    [selectedIds, bulkResolveJobs]
  );

  const handleOverwriteSelected = useCallback(() => {
    setPendingBulkAction('Overwrite');
  }, []);

  const handleKeepBothSelected = useCallback(
    () => startBulkResolve('KeepBoth'),
    [startBulkResolve]
  );

  const handleDiscardSelected = useCallback(() => {
    setPendingBulkAction('Discard');
  }, []);

  const handleBulkResolveConfirmed = useCallback(() => {
    const action = pendingBulkAction;

    setPendingBulkAction(null);

    if (action) {
      startBulkResolve(action);
    }
  }, [pendingBulkAction, startBulkResolve]);

  const handleBulkResolveCancelled = useCallback(() => {
    setPendingBulkAction(null);
  }, []);

  const bulkConfirmMessage =
    pendingBulkAction === 'Discard'
      ? translate('TranscodeBulkDiscardConfirmMessage', {
          count: selectedIds.length,
        })
      : translate('TranscodeBulkOverwriteConfirmMessage', {
          count: selectedIds.length,
        });

  const handleClearHistoryPress = useCallback(() => {
    setIsClearHistoryModalOpen(true);
  }, []);

  const handleClearHistoryModalClose = useCallback(() => {
    setIsClearHistoryModalOpen(false);
  }, []);

  const handleClearHistoryConfirmed = useCallback(() => {
    clearHistory(undefined, {
      onSuccess: ({ clearedCount }) => {
        setIsClearHistoryModalOpen(false);

        showMessage({
          id: CLEAR_HISTORY_MESSAGE_ID,
          name: translate('Transcoding'),
          message: translate('TranscodeHistoryCleared', {
            count: clearedCount,
          }),
          type: SUCCESS,
          hideAfter: 4,
        });
      },
      onError: () => {
        setIsClearHistoryModalOpen(false);

        showMessage({
          id: CLEAR_HISTORY_MESSAGE_ID,
          name: translate('Transcoding'),
          message: translate('TranscodeHistoryClearFailed'),
          type: ERROR,
          hideAfter: 10,
        });
      },
    });
  }, [clearHistory]);

  const handleRequeueFailed = useCallback(() => {
    if (!requeueCount) {
      return;
    }

    createJobs(
      {
        force: true,
        requeueJobIds,
      },
      {
        onSuccess: (response) => {
          const queued = response?.jobs?.length ?? 0;
          const skipped = response?.skippedCount ?? 0;

          showMessage({
            id: REQUEUE_MESSAGE_ID,
            name: translate('Transcoding'),
            message: skipped
              ? translate('RequeueFailedPartial', {
                  queued,
                  total: requeueCount,
                })
              : translate('RequeueFailedComplete', { count: queued }),
            type: queued ? SUCCESS : WARNING,
            hideAfter: queued ? 4 : 10,
          });
        },
        onError: () => {
          showMessage({
            id: REQUEUE_MESSAGE_ID,
            name: translate('Transcoding'),
            message: translate('RequeueFailedError'),
            type: ERROR,
            hideAfter: 10,
          });
        },
      }
    );
  }, [createJobs, requeueJobIds, requeueCount]);

  return (
    <PageContent title={translate('Transcoding')}>
      <PageContentBody>
        {isFetching && !isFetched ? <LoadingIndicator /> : null}

        {error ? (
          <Alert kind={kinds.DANGER}>
            <div className="flex items-center gap-[10px]">
              <span>{translate('CompressionJobsLoadError')}</span>

              <SpinnerButton
                size={sizes.SMALL}
                isSpinning={isFetching}
                onPress={handleRetry}
              >
                {translate('TranscodeJobsRetry')}
              </SpinnerButton>
            </div>
          </Alert>
        ) : null}

        {isFetched && (!error || jobs.length > 0) ? (
          <>
            <FieldSet legend={translate('CompressionActive')}>
              {active.length ? (
                renderJobGroups(activeGroups)
              ) : (
                <div className={EMPTY_CLASS}>
                  {translate('CompressionNoActiveJobs')}
                </div>
              )}
            </FieldSet>

            <FieldSet legend={translate('Transferring')}>
              {transferring.length ? (
                renderJobGroups(transferringGroups)
              ) : (
                <div className={EMPTY_CLASS}>
                  {translate('CompressionNoTransferringJobs')}
                </div>
              )}
            </FieldSet>

            <FieldSet legend={translate('CompressionReview')}>
              {review.length ? (
                <>
                  <div className="mb-[10px] flex flex-wrap items-center gap-[10px]">
                    <label className="flex items-center gap-[5px]">
                      <input
                        ref={selectAllRef}
                        type="checkbox"
                        checked={allSelected}
                        onChange={handleSelectAll}
                      />

                      {translate('SelectAll')}
                    </label>

                    <SpinnerButton
                      kind={kinds.PRIMARY}
                      isSpinning={!!bulk}
                      isDisabled={!selectedIds.length}
                      onPress={handleOverwriteSelected}
                    >
                      {translate('OverwriteSelected')}
                    </SpinnerButton>

                    <SpinnerButton
                      isSpinning={!!bulk}
                      isDisabled={!selectedIds.length}
                      onPress={handleKeepBothSelected}
                    >
                      {translate('KeepBothSelected')}
                    </SpinnerButton>

                    <SpinnerButton
                      kind={kinds.DANGER}
                      isSpinning={!!bulk}
                      isDisabled={!selectedIds.length}
                      onPress={handleDiscardSelected}
                    >
                      {translate('DiscardSelected')}
                    </SpinnerButton>
                  </div>

                  {bulk ? (
                    <div className="mb-[10px]">
                      <div className={`mb-[5px] ${EMPTY_CLASS}`}>
                        {translate('BulkResolveProgress', {
                          done: Math.min(bulkDone, bulk.total),
                          total: bulk.total,
                        })}
                      </div>

                      <ProgressBar
                        ariaLabel={translate('BulkResolveProgress', {
                          done: Math.min(bulkDone, bulk.total),
                          total: bulk.total,
                        })}
                        progress={bulkProgress}
                        showText={true}
                      />
                    </div>
                  ) : null}

                  {reviewGroups.map((group) => (
                    <React.Fragment key={group.key}>
                      <h3 className={GROUP_CLASS}>{group.title}</h3>

                      {group.seasons.map((season) => (
                        <React.Fragment key={season.key}>
                          {season.label ? (
                            <h4 className={SEASON_CLASS}>{season.label}</h4>
                          ) : null}

                          {season.jobs.map((job) => (
                            <TranscodeJobRow
                              key={job.id}
                              job={job}
                              deviceName={getDeviceName(job.deviceId)}
                              profileName={getProfileName(job.profileId)}
                              isSelected={selectedSet.has(job.id)}
                              isBulkRunning={!!bulk}
                              onSelect={handleSelect}
                            />
                          ))}
                        </React.Fragment>
                      ))}
                    </React.Fragment>
                  ))}
                </>
              ) : (
                <div className={EMPTY_CLASS}>
                  {translate('CompressionNoReviewJobs')}
                </div>
              )}
            </FieldSet>

            <FieldSet legend={translate('History')}>
              {history.length ? (
                <>
                  <div className="mb-[10px] flex justify-end gap-[10px]">
                    <SpinnerButton
                      size={sizes.SMALL}
                      isSpinning={isCreating}
                      isDisabled={!requeueCount || isCreating}
                      onPress={handleRequeueFailed}
                    >
                      {translate('RequeueAllFailed')}
                    </SpinnerButton>

                    <SpinnerButton
                      size={sizes.SMALL}
                      kind={kinds.DANGER}
                      isSpinning={isClearingHistory}
                      onPress={handleClearHistoryPress}
                    >
                      {translate('ClearHistory')}
                    </SpinnerButton>
                  </div>

                  <div className="max-h-[420px] overflow-y-auto rounded-[4px] border border-[var(--borderColor)]">
                    {historyGroups.map((group) => (
                      <React.Fragment key={group.key}>
                        <h3 className={`${GROUP_CLASS} px-[6px]`}>
                          {group.title}
                        </h3>

                        {group.seasons.map((season) => (
                          <React.Fragment key={season.key}>
                            {season.label ? (
                              <h4 className={`${SEASON_CLASS} px-[6px]`}>
                                {season.label}
                              </h4>
                            ) : null}

                            {season.jobs.map((job) => (
                              <TranscodeHistoryRow
                                key={job.id}
                                job={job}
                                deviceName={getDeviceName(job.deviceId)}
                                profileName={getProfileName(job.profileId)}
                              />
                            ))}
                          </React.Fragment>
                        ))}
                      </React.Fragment>
                    ))}
                  </div>
                </>
              ) : (
                <div className={EMPTY_CLASS}>
                  {translate('CompressionNoHistory')}
                </div>
              )}
            </FieldSet>

            <ConfirmModal
              isOpen={isClearHistoryModalOpen}
              kind={kinds.DANGER}
              title={translate('ClearHistory')}
              message={translate('ClearHistoryConfirm', {
                count: history.length,
              })}
              confirmLabel={translate('ClearHistory')}
              isSpinning={isClearingHistory}
              onConfirm={handleClearHistoryConfirmed}
              onCancel={handleClearHistoryModalClose}
            />

            <ConfirmModal
              isOpen={pendingBulkAction !== null}
              kind={kinds.DANGER}
              title={translate('CompressionReview')}
              message={bulkConfirmMessage}
              confirmLabel={
                pendingBulkAction === 'Discard'
                  ? translate('Discard')
                  : translate('OverwriteOriginal')
              }
              onConfirm={handleBulkResolveConfirmed}
              onCancel={handleBulkResolveCancelled}
            />
          </>
        ) : null}
      </PageContentBody>
    </PageContent>
  );
}

export default MediaCompressionPage;
