import React, { useCallback, useEffect, useState } from 'react';
import CheckInput from 'Components/Form/CheckInput';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import ProgressBar from 'Components/ProgressBar';
import { kinds } from 'Helpers/Props';
import { CheckInputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import {
  getRateControlLabel,
  getTranscodeStatusClass,
  getTranscodeStatusLabel,
} from './transcodeStatus';
import {
  TranscodeJob,
  useCancelTranscodeJob,
  useResolveTranscodeJob,
} from './useTranscodeJobs';

const ROW_CLASS = 'mb-[10px] rounded-[4px] border border-[#e5e5e5] p-[10px]';
const META_CLASS = 'text-[var(--helpTextColor)]';

interface TranscodeJobRowProps {
  job: TranscodeJob;
  deviceName?: string;
  profileName?: string;
  isSelected?: boolean;
  isBulkRunning?: boolean;
  onSelect?(jobId: number, isSelected: boolean, shiftKey: boolean): void;
}

function TranscodeJobRow({
  job,
  deviceName,
  profileName,
  isSelected = false,
  isBulkRunning = false,
  onSelect,
}: TranscodeJobRowProps) {
  const { cancelJob, isCancelling, cancelError, resetCancelError } =
    useCancelTranscodeJob();
  const { resolveJob, isResolving, resolveError, resetResolveError } =
    useResolveTranscodeJob();

  const [confirmAction, setConfirmAction] = useState<
    'Overwrite' | 'Discard' | null
  >(null);

  const actionError = cancelError ?? resolveError;
  const sourceName = job.sourcePath.split(/[\\/]/).pop() || job.sourcePath;
  const planLabel =
    profileName ?? (job.maxHeight ? `${job.maxHeight}p` : undefined);

  const isActive = job.status === 'Queued' || job.status === 'Running';
  const isReview = job.status === 'AwaitingReview';
  const isRunning = job.status === 'Running';
  const isTransferring = job.status === 'Transferring';

  // The jobs query polls every 3s; a transient action failure must not stay pinned to the row
  // forever. Clear it whenever the poll reports a new status/timestamp.
  useEffect(() => {
    resetCancelError();
    resetResolveError();
  }, [job.status, job.lastUpdatedAt, resetCancelError, resetResolveError]);

  const handleCancel = useCallback(
    () => cancelJob({ jobId: job.id }),
    [cancelJob, job.id]
  );
  const handleKeepBoth = useCallback(
    () => resolveJob({ jobId: job.id, action: 'KeepBoth' }),
    [resolveJob, job.id]
  );
  const handleOverwritePress = useCallback(
    () => setConfirmAction('Overwrite'),
    []
  );
  const handleDiscardPress = useCallback(() => setConfirmAction('Discard'), []);
  const handleConfirmClose = useCallback(() => setConfirmAction(null), []);
  const handleConfirmAction = useCallback(() => {
    const action = confirmAction;

    setConfirmAction(null);

    if (action === 'Overwrite') {
      resolveJob({ jobId: job.id, action: 'Overwrite' });
    } else if (action === 'Discard') {
      resolveJob({ jobId: job.id, action: 'Discard' });
    }
  }, [confirmAction, resolveJob, job.id]);

  const handleSelectChange = useCallback(
    ({ value, shiftKey }: CheckInputChanged) => {
      onSelect?.(job.id, value, shiftKey);
    },
    [job.id, onSelect]
  );

  return (
    <div className={ROW_CLASS}>
      <div className="flex flex-wrap items-center justify-between gap-[10px]">
        <div className="flex items-center gap-[10px]">
          {isReview && onSelect ? (
            <CheckInput
              className="m-0!"
              containerClassName="relative flex flex-[0_0_auto] select-none"
              name={String(job.id)}
              ariaLabel={translate('SelectTranscodeJobNamed', {
                name: `${sourceName} (#${job.id})`,
              })}
              value={isSelected}
              onChange={handleSelectChange}
            />
          ) : null}

          <span className="break-all font-semibold">{job.sourcePath}</span>
        </div>

        <span
          className={`${META_CLASS} ${getTranscodeStatusClass(job.status)}`}
        >
          {getTranscodeStatusLabel(job.status)}
        </span>
      </div>

      {isRunning || isTransferring ? (
        <div className="mt-[5px]">
          <ProgressBar
            ariaLabel={translate('TranscodeJobProgress', { name: sourceName })}
            progress={job.progress}
            showText={true}
          />
        </div>
      ) : null}

      <div className={`mt-[5px] ${META_CLASS}`}>
        {planLabel ? `${planLabel} · ` : ''}
        {job.deviceId
          ? `${translate('TranscodeDevices')}: ${deviceName ?? job.deviceId} · `
          : ''}
        {job.videoCodec ? `${job.videoCodec.toUpperCase()} · ` : ''}
        {job.rateControl ? `${getRateControlLabel(job.rateControl)} · ` : ''}
        {job.sourceSize
          ? `${formatBytes(job.sourceSize)}${
              job.outputSize ? ` → ${formatBytes(job.outputSize)}` : ''
            }`
          : ''}
        {isRunning && job.speed ? ` · ${job.speed}` : ''}
        {isTransferring && job.speed ? ` · ${job.speed}` : ''}
        {isRunning && job.eta ? ` · ${translate('Eta')} ${job.eta}` : ''}
      </div>

      {job.error ? (
        <div className="mt-[5px] text-[var(--alertDangerColor)]">
          {job.error}
        </div>
      ) : null}

      {actionError ? (
        <div className="mt-[5px] text-[var(--alertDangerColor)]">
          {translate('TranscodeActionFailed')}
        </div>
      ) : null}

      {isReview && job.message ? (
        <div className={`mt-[5px] ${META_CLASS}`}>{job.message}</div>
      ) : null}

      {isActive || isTransferring ? (
        <div className="mt-[10px] flex gap-[10px]">
          <SpinnerButton
            kind={kinds.DANGER}
            isSpinning={isCancelling}
            onPress={handleCancel}
          >
            {isTransferring ? translate('CancelTransfer') : translate('Cancel')}
          </SpinnerButton>
        </div>
      ) : null}

      {isReview ? (
        <div className="mt-[10px] flex gap-[10px]">
          <SpinnerButton
            kind={kinds.PRIMARY}
            isSpinning={isResolving}
            isDisabled={isBulkRunning}
            onPress={handleOverwritePress}
          >
            {translate('OverwriteOriginal')}
          </SpinnerButton>

          <SpinnerButton
            isSpinning={isResolving}
            isDisabled={isBulkRunning}
            onPress={handleKeepBoth}
          >
            {translate('KeepBoth')}
          </SpinnerButton>

          <SpinnerButton
            kind={kinds.DANGER}
            isSpinning={isResolving}
            isDisabled={isBulkRunning}
            onPress={handleDiscardPress}
          >
            {translate('Discard')}
          </SpinnerButton>
        </div>
      ) : null}

      <ConfirmModal
        isOpen={confirmAction !== null}
        kind={kinds.DANGER}
        title={
          confirmAction === 'Discard'
            ? translate('Discard')
            : translate('OverwriteOriginal')
        }
        message={
          confirmAction === 'Discard'
            ? translate('TranscodeDiscardConfirmMessage')
            : translate('TranscodeOverwriteConfirmMessage')
        }
        confirmLabel={
          confirmAction === 'Discard'
            ? translate('Discard')
            : translate('OverwriteOriginal')
        }
        isSpinning={isResolving}
        onConfirm={handleConfirmAction}
        onCancel={handleConfirmClose}
      />
    </div>
  );
}

export default TranscodeJobRow;
