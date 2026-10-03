import React, { useCallback, useEffect, useId, useMemo, useState } from 'react';
import { showMessage } from 'App/messagesStore';
import Alert from 'Components/Alert';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import { WARNING } from 'Helpers/Props/messageTypes';
import { useCompressionSettings } from 'Settings/Compression/useCompressionSettings';
import {
  getDeviceLabel,
  TranscodeDevice,
  useMediaCompressionCapabilities,
} from 'Settings/Compression/useMediaCompressionCapabilities';
import {
  TranscodeProfile,
  useTranscodeProfiles,
} from 'Settings/Compression/useTranscodeProfiles';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import {
  CompressMediaType,
  getCompressDeviceKey,
  getCompressOption,
  getCompressProfileKey,
  setCompressOption,
} from './compressOptionsStore';
import { TranscodeRequest, useCreateTranscodeJobs } from './useTranscodeJobs';

export interface CompressModalContentProps {
  episodeFileIds?: number[];
  movieFileIds?: number[];
  seriesIds?: number[];
  seasonNumbers?: number[];
  movieIds?: number[];
  onModalClose(): void;
}

const CODECS = ['hevc', 'h264', 'av1'];
const CONTAINERS = ['mkv', 'mp4', 'mov'];
const RESOLUTIONS = ['2160', '1440', '1080', '720'];
const MODES = [
  { value: 'quality', label: 'CompressionModeQuality' },
  { value: 'targetSize', label: 'CompressionModeTargetSize' },
  { value: 'percentageReduction', label: 'CompressionModePercentage' },
  { value: 'remux', label: 'CompressionModeRemux' },
];

const SELECT_CLASS =
  'w-full rounded-[4px] border border-[#e5e5e5] bg-[var(--inputBackgroundColor)] px-[8px] py-[6px] text-[var(--textColor)]';
const ROW_CLASS = 'mb-[15px]';
const META_CLASS = 'text-[var(--helpTextColor)]';
const QUEUE_MESSAGE_ID = -1011;

function codecToken(codec: string) {
  if (codec === 'h264') {
    return '264';
  }

  if (codec === 'hevc') {
    return '265';
  }

  return 'av1';
}

function deviceAllowsCodec(device: TranscodeDevice, codec: string) {
  const settings = device.codecs ?? [];

  if (settings.length) {
    return settings.some(
      (setting) => setting.codec === codec && setting.enabled
    );
  }

  const encoders = device.capabilities?.encoders ?? [];
  const token = codecToken(codec);

  return encoders.some(
    (encoder) => encoder.includes(token) || encoder.includes(codec)
  );
}

function describeProfile(profile: TranscodeProfile) {
  const extras = `${profile.maxHeight ? ` · ${profile.maxHeight}p` : ''}${
    profile.tag ? ` · [${profile.tag}]` : ''
  }${profile.preferEnglishAudio ? ` · ${translate('EnglishAudioFirst')}` : ''}`;

  if (profile.mode === 'Remux') {
    return `Remux → ${(profile.container ?? 'mkv').toUpperCase()}${extras}`;
  }

  if (profile.mode === 'TargetSize') {
    return `${profile.codec?.toUpperCase()} · ${
      profile.targetSizeMB ?? '?'
    } MB${extras}`;
  }

  if (profile.mode === 'PercentageReduction') {
    return `${profile.codec?.toUpperCase()} · -${
      profile.targetPercent ?? '?'
    }%${extras}`;
  }

  return `${profile.codec?.toUpperCase()} · CRF ${
    profile.qualityValue
  }${extras}`;
}

function CompressModalContent(props: CompressModalContentProps) {
  const {
    episodeFileIds,
    movieFileIds,
    seriesIds,
    seasonNumbers,
    movieIds,
    onModalClose,
  } = props;

  const { capabilities } = useMediaCompressionCapabilities();
  const { profiles } = useTranscodeProfiles();
  const { settings } = useCompressionSettings();
  const { createJobs, isCreating } = useCreateTranscodeJobs();

  const isMovieRequest =
    (movieFileIds?.length ?? 0) > 0 || (movieIds?.length ?? 0) > 0;
  const mediaType: CompressMediaType = isMovieRequest ? 'movie' : 'series';
  const profileKey = getCompressProfileKey(mediaType);
  const deviceKey = getCompressDeviceKey(mediaType);

  const [profileId, setProfileId] = useState(() =>
    getCompressOption(profileKey)
  );
  const [profileInitialized, setProfileInitialized] = useState(false);
  const [deviceId, setDeviceId] = useState(() => getCompressOption(deviceKey));
  const [codec, setCodec] = useState('hevc');
  const [mode, setMode] = useState('quality');
  const [container, setContainer] = useState('mkv');
  const [quality, setQuality] = useState('');
  const [targetSize, setTargetSize] = useState('');
  const [targetPercent, setTargetPercent] = useState('');
  const [maxHeight, setMaxHeight] = useState('');
  const [preset, setPreset] = useState('');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const formId = useId();

  const devices = useMemo(
    () =>
      (capabilities.devices ?? []).filter(
        (device) => device.enabled && !device.unavailable
      ),
    [capabilities.devices]
  );

  // Preselect the last used profile (falling back to the default) so a transcode is one press;
  // "Custom" (an empty id) is remembered too. The choice is scoped per media type so a movie
  // profile is not reused for a series. The dialog unmounts while closed, so without this the
  // choice resets to the default every time it is reopened.
  useEffect(() => {
    if (profileInitialized || !profiles.length) {
      return;
    }

    const remembered = getCompressOption(profileKey);
    const rememberedIsValid =
      remembered === '' ||
      profiles.some((profile) => String(profile.id) === remembered);

    if (rememberedIsValid) {
      setProfileId(remembered);
    } else {
      const preferred =
        profiles.find((profile) => profile.isDefault) ?? profiles[0];

      setProfileId(String(preferred.id));
      setCompressOption(profileKey, String(preferred.id));
    }

    setProfileInitialized(true);
  }, [profiles, profileInitialized, profileKey]);

  const selectedProfile = profiles.find(
    (profile) => String(profile.id) === profileId
  );
  const selectedDevice = devices.find((device) => device.deviceId === deviceId);

  // A remembered device may have been disabled/removed since it was chosen; fall back to Auto.
  useEffect(() => {
    if (
      deviceId &&
      devices.length &&
      !devices.some((device) => device.deviceId === deviceId)
    ) {
      setDeviceId('');
      setCompressOption(deviceKey, '');
    }
  }, [deviceId, deviceKey, devices]);

  const availableCodecs = useMemo(
    () =>
      CODECS.filter((candidate) =>
        selectedDevice
          ? deviceAllowsCodec(selectedDevice, candidate)
          : devices.some((device) => deviceAllowsCodec(device, candidate))
      ),
    [selectedDevice, devices]
  );

  const hasSupportedCodec = availableCodecs.length > 0;

  const defaultTargetSizeMB = isMovieRequest
    ? settings?.defaultTargetMovieSizeMB
    : settings?.defaultTargetEpisodeSizeMB;
  const defaultQualityValue = settings?.defaultQualityValue;
  const defaultQuality =
    defaultQualityValue === undefined ? undefined : String(defaultQualityValue);
  const defaultReducePercent = settings?.defaultReducePercent;
  const defaultPreset = settings?.defaultPreset || undefined;

  useEffect(() => {
    if (
      !selectedProfile &&
      hasSupportedCodec &&
      !availableCodecs.includes(codec)
    ) {
      setCodec(availableCodecs[0]);
    }
  }, [selectedProfile, hasSupportedCodec, availableCodecs, codec]);

  const handleProfileChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setProfileId(event.target.value);
      setCompressOption(profileKey, event.target.value);
    },
    [profileKey]
  );

  const handleDeviceChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setDeviceId(event.target.value);
      setCompressOption(deviceKey, event.target.value);
    },
    [deviceKey]
  );

  const handleCodecChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setCodec(event.target.value),
    []
  );

  const handleModeChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setMode(event.target.value),
    []
  );

  const handleContainerChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setContainer(event.target.value),
    []
  );

  const handleQualityChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setQuality(event.target.value),
    []
  );

  const handleTargetSizeChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setTargetSize(event.target.value),
    []
  );

  const handleTargetPercentChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setTargetPercent(event.target.value),
    []
  );

  const handleMaxHeightChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setMaxHeight(event.target.value),
    []
  );

  const handlePresetChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setPreset(event.target.value),
    []
  );

  const requestedFileCount =
    (episodeFileIds?.length ?? 0) + (movieFileIds?.length ?? 0);

  const handleSavePress = useCallback(() => {
    if (!selectedProfile && mode === 'targetSize' && !targetSize) {
      setErrorMessage(translate('CompressionTargetSizeRequired'));

      return;
    }

    if (!selectedProfile && mode === 'percentageReduction' && !targetPercent) {
      setErrorMessage(translate('CompressionTargetPercentRequired'));

      return;
    }

    const request: TranscodeRequest = {
      episodeFileIds,
      movieFileIds,
      seriesIds,
      seasonNumbers,
      movieIds,
    };

    if (selectedProfile) {
      request.profileId = selectedProfile.id;
    } else {
      request.mode = mode;

      if (mode === 'remux') {
        request.container = container;
      } else {
        request.codec = codec;

        if (quality) {
          request.quality = Number(quality);
        }

        if (preset) {
          request.preset = preset;
        }

        if (mode === 'targetSize' && targetSize) {
          request.targetSize = Number(targetSize) * 1024 * 1024;
        }

        if (mode === 'percentageReduction' && targetPercent) {
          request.targetPercent = Number(targetPercent);
        }

        if (maxHeight) {
          request.maxHeight = Number(maxHeight);
        }
      }
    }

    if (deviceId) {
      request.deviceId = deviceId;
    }

    setErrorMessage(null);

    createJobs(request, {
      onSuccess: (response) => {
        const queued = response?.jobs?.length ?? 0;
        const requested = response?.requestedCount ?? requestedFileCount;
        const skipped =
          response?.skippedCount ?? Math.max(requested - queued, 0);
        const savings = formatBytes(response?.projectedSavingsBytes ?? 0);

        // Order matters: a 1-file request that queues nothing must show the "nothing queued"
        // message rather than "Queued 0 of 1". A fully queued request stays silent.
        if (queued === 0) {
          showMessage({
            id: QUEUE_MESSAGE_ID,
            name: translate('Transcoding'),
            message: translate('CompressionQueueNone'),
            type: WARNING,
            hideAfter: 10,
          });
        } else if (skipped > 0) {
          showMessage({
            id: QUEUE_MESSAGE_ID,
            name: translate('Transcoding'),
            message: translate('CompressionQueuePartial', {
              queued,
              requested,
              skipped,
              savings,
            }),
            type: WARNING,
            hideAfter: 10,
          });
        }

        onModalClose();
      },
      onError: (error) => {
        setErrorMessage(
          error?.statusBody?.message ||
            error?.message ||
            translate('CompressionQueueFailed')
        );
      },
    });
  }, [
    episodeFileIds,
    movieFileIds,
    seriesIds,
    seasonNumbers,
    movieIds,
    selectedProfile,
    codec,
    mode,
    container,
    deviceId,
    quality,
    preset,
    targetSize,
    targetPercent,
    maxHeight,
    requestedFileCount,
    createJobs,
    onModalClose,
  ]);

  const profileLabel = (profile: TranscodeProfile) =>
    profile.isDefault
      ? `${profile.name} (${translate('Default')})`
      : profile.name;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('CompressMedia')}</ModalHeader>

      <ModalBody>
        {errorMessage ? (
          <Alert kind={kinds.DANGER}>{errorMessage}</Alert>
        ) : null}

        <div className={ROW_CLASS}>
          <label
            htmlFor={`${formId}-profile`}
            className="mb-[5px] block font-bold"
          >
            {translate('TranscodeProfile')}
          </label>

          <select
            id={`${formId}-profile`}
            className={SELECT_CLASS}
            value={profileId}
            onChange={handleProfileChange}
          >
            <option value="">{translate('Custom')}</option>

            {profiles.map((profile) => (
              <option key={profile.id} value={String(profile.id)}>
                {profileLabel(profile)}
              </option>
            ))}
          </select>

          {selectedProfile ? (
            <div className={`mt-[5px] ${META_CLASS}`}>
              {describeProfile(selectedProfile)}
            </div>
          ) : null}
        </div>

        <div className={ROW_CLASS}>
          <label
            htmlFor={`${formId}-device`}
            className="mb-[5px] block font-bold"
          >
            {translate('TranscodeDevices')}
          </label>

          <select
            id={`${formId}-device`}
            className={SELECT_CLASS}
            value={deviceId}
            onChange={handleDeviceChange}
          >
            <option value="">{translate('Auto')}</option>

            {devices.map((device) => (
              <option key={device.deviceId} value={device.deviceId}>
                {getDeviceLabel(device)}
              </option>
            ))}
          </select>
        </div>

        {selectedProfile ? null : (
          <>
            <div className={ROW_CLASS}>
              <label
                htmlFor={`${formId}-codec`}
                className="mb-[5px] block font-bold"
              >
                {translate('VideoCodec')}
              </label>

              <select
                id={`${formId}-codec`}
                className={SELECT_CLASS}
                value={codec}
                disabled={!hasSupportedCodec}
                onChange={handleCodecChange}
              >
                {CODECS.map((candidate) => (
                  <option key={candidate} value={candidate}>
                    {candidate.toUpperCase()}
                  </option>
                ))}
              </select>

              {hasSupportedCodec ? null : (
                <div className="mt-[5px]">
                  <Alert kind={kinds.WARNING}>
                    {translate('CompressionCodecUnsupported')}
                  </Alert>
                </div>
              )}
            </div>

            <div className={ROW_CLASS}>
              <label
                htmlFor={`${formId}-mode`}
                className="mb-[5px] block font-bold"
              >
                {translate('CompressionMode')}
              </label>

              <select
                id={`${formId}-mode`}
                className={SELECT_CLASS}
                value={mode}
                onChange={handleModeChange}
              >
                {MODES.map((item) => (
                  <option key={item.value} value={item.value}>
                    {translate(item.label)}
                  </option>
                ))}
              </select>
            </div>

            {mode === 'remux' ? (
              <div className={ROW_CLASS}>
                <label
                  htmlFor={`${formId}-container`}
                  className="mb-[5px] block font-bold"
                >
                  {translate('Container')}
                </label>

                <select
                  id={`${formId}-container`}
                  className={SELECT_CLASS}
                  value={container}
                  onChange={handleContainerChange}
                >
                  {CONTAINERS.map((item) => (
                    <option key={item} value={item}>
                      {item.toUpperCase()}
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <>
                <div className={ROW_CLASS}>
                  <label
                    htmlFor={`${formId}-resolution`}
                    className="mb-[5px] block font-bold"
                  >
                    {translate('Resolution')}
                  </label>

                  <select
                    id={`${formId}-resolution`}
                    className={SELECT_CLASS}
                    value={maxHeight}
                    onChange={handleMaxHeightChange}
                  >
                    <option value="">
                      {translate('KeepSourceResolution')}
                    </option>

                    {RESOLUTIONS.map((resolution) => (
                      <option key={resolution} value={resolution}>
                        {resolution}p
                      </option>
                    ))}
                  </select>
                </div>

                {mode === 'targetSize' ? (
                  <div className={ROW_CLASS}>
                    <label
                      htmlFor={`${formId}-target-size`}
                      className="mb-[5px] block font-bold"
                    >
                      {translate('CompressionTargetSize')}
                    </label>

                    <input
                      id={`${formId}-target-size`}
                      className={SELECT_CLASS}
                      type="number"
                      min={1}
                      placeholder={
                        defaultTargetSizeMB === undefined
                          ? undefined
                          : String(defaultTargetSizeMB)
                      }
                      value={targetSize}
                      onChange={handleTargetSizeChange}
                    />

                    {defaultTargetSizeMB === undefined ? null : (
                      <div className={`mt-[5px] ${META_CLASS}`}>
                        {translate('CompressionTargetSizeDefault', {
                          size: defaultTargetSizeMB,
                        })}
                      </div>
                    )}
                  </div>
                ) : null}

                {mode === 'percentageReduction' ? (
                  <div className={ROW_CLASS}>
                    <label
                      htmlFor={`${formId}-target-percent`}
                      className="mb-[5px] block font-bold"
                    >
                      {translate('CompressionTargetPercent')}
                    </label>

                    <input
                      id={`${formId}-target-percent`}
                      className={SELECT_CLASS}
                      type="number"
                      min={1}
                      max={99}
                      placeholder={
                        defaultReducePercent === undefined
                          ? undefined
                          : String(defaultReducePercent)
                      }
                      value={targetPercent}
                      onChange={handleTargetPercentChange}
                    />

                    {defaultReducePercent === undefined ? null : (
                      <div className={`mt-[5px] ${META_CLASS}`}>
                        {translate('CompressionTargetPercentDefault', {
                          percent: defaultReducePercent,
                        })}
                      </div>
                    )}
                  </div>
                ) : null}

                {mode === 'quality' ? (
                  <div className={ROW_CLASS}>
                    <label
                      htmlFor={`${formId}-quality`}
                      className="mb-[5px] block font-bold"
                    >
                      {translate('CompressionQuality')}
                    </label>

                    <input
                      id={`${formId}-quality`}
                      className={SELECT_CLASS}
                      type="number"
                      min={0}
                      placeholder={defaultQuality}
                      value={quality}
                      onChange={handleQualityChange}
                    />
                  </div>
                ) : null}

                <div className={ROW_CLASS}>
                  <label
                    htmlFor={`${formId}-preset`}
                    className="mb-[5px] block font-bold"
                  >
                    {translate('Preset')}
                  </label>

                  <input
                    id={`${formId}-preset`}
                    className={SELECT_CLASS}
                    type="text"
                    placeholder={defaultPreset ?? translate('Preset')}
                    value={preset}
                    onChange={handlePresetChange}
                  />
                </div>
              </>
            )}
          </>
        )}
      </ModalBody>

      <ModalFooter>
        <SpinnerButton
          isSpinning={false}
          isDisabled={isCreating}
          onPress={onModalClose}
        >
          {translate('Cancel')}
        </SpinnerButton>

        <SpinnerButton
          kind={kinds.PRIMARY}
          isSpinning={isCreating}
          onPress={handleSavePress}
        >
          {translate('CompressMedia')}
        </SpinnerButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default CompressModalContent;
