import translate from 'Utilities/String/translate';

const STATUS_LABEL_KEYS: Record<string, string> = {
  Queued: 'Queued',
  Running: 'TranscodeStatusRunning',
  AwaitingReview: 'CompressionReview',
  Transferring: 'Transferring',
  Completed: 'Completed',
  Failed: 'Failed',
  Cancelled: 'TranscodeStatusCancelled',
  Skipped: 'TranscodeStatusSkipped',
};

const RATE_CONTROL_LABEL_KEYS: Record<string, string> = {
  'constant-quality': 'TranscodeRateControlConstantQuality',
  vbr: 'TranscodeRateControlVbr',
};

const STATUS_CLASSES: Record<string, string> = {
  Completed: 'text-[var(--alertSuccessColor)]',
  Failed: 'text-[var(--alertDangerColor)]',
  Cancelled: 'text-[var(--helpTextColor)]',
  Skipped: 'text-[var(--alertWarningColor)]',
};

export function getTranscodeStatusLabel(status: string) {
  const key = STATUS_LABEL_KEYS[status];

  return key ? translate(key) : status;
}

export function getTranscodeStatusClass(status: string) {
  return STATUS_CLASSES[status] ?? '';
}

export function getRateControlLabel(rateControl?: string) {
  if (!rateControl) {
    return '';
  }

  const key = RATE_CONTROL_LABEL_KEYS[rateControl];

  return key ? translate(key) : rateControl;
}
