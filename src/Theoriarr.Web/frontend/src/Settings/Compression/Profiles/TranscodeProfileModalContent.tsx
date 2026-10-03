import React, { useCallback, useEffect, useId, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { useCompressionSettings } from '../useCompressionSettings';
import {
  getDeviceLabel,
  TranscodeDevice,
} from '../useMediaCompressionCapabilities';
import { TranscodeProfile } from '../useTranscodeProfiles';

export interface TranscodeProfileModalContentProps {
  profile?: TranscodeProfile;
  devices: TranscodeDevice[];
  isSaving: boolean;
  error?: string | null;
  onSave(profile: TranscodeProfile): void;
  onModalClose(): void;
}

const CODECS = ['hevc', 'h264', 'av1'];
const CONTAINERS = ['mkv', 'mp4', 'mov'];
const RESOLUTIONS = ['2160', '1440', '1080', '720'];
const MODES = [
  { value: 'quality', label: 'CompressionModeQuality' },
  { value: 'targetSize', label: 'CompressionModeTargetSize' },
  { value: 'percentageReduction', label: 'CompressionModePercentage' },
  { value: 'remux', label: 'Remux' },
];

const SELECT_CLASS =
  'w-full rounded-[4px] border border-[#e5e5e5] bg-[var(--inputBackgroundColor)] px-[8px] py-[6px] text-[var(--textColor)]';
const ROW_CLASS = 'mb-[15px]';
const META_CLASS = 'text-[var(--helpTextColor)]';

function modeToken(mode?: string) {
  switch (mode) {
    case 'TargetSize':
      return 'targetSize';
    case 'PercentageReduction':
      return 'percentageReduction';
    case 'Remux':
      return 'remux';
    default:
      return 'quality';
  }
}

function TranscodeProfileModalContent(
  props: TranscodeProfileModalContentProps
) {
  const { profile, devices, isSaving, error, onSave, onModalClose } = props;
  const { settings } = useCompressionSettings();

  const formId = useId();

  const [name, setName] = useState(profile?.name ?? '');
  const [mode, setMode] = useState(modeToken(profile?.mode));
  const [codec, setCodec] = useState(profile?.codec ?? 'hevc');
  const [container, setContainer] = useState(profile?.container ?? 'mkv');
  const [quality, setQuality] = useState(
    profile?.qualityValue ? String(profile.qualityValue) : ''
  );
  const [preset, setPreset] = useState(profile?.preset ?? '');
  const [maxHeight, setMaxHeight] = useState(
    profile?.maxHeight ? String(profile.maxHeight) : ''
  );
  const [tag, setTag] = useState(profile?.tag ?? '');
  const [preferEnglishAudio, setPreferEnglishAudio] = useState(
    profile?.preferEnglishAudio ?? false
  );
  const [targetSize, setTargetSize] = useState(
    profile?.targetSizeMB ? String(profile.targetSizeMB) : ''
  );
  const [targetPercent, setTargetPercent] = useState(
    profile?.targetPercent ? String(profile.targetPercent) : ''
  );
  const [deviceId, setDeviceId] = useState(profile?.deviceId ?? '');
  const [isDefault, setIsDefault] = useState(profile?.isDefault ?? false);
  const [validationError, setValidationError] = useState<string | null>(null);

  const selectableDevices = useMemo(
    () => devices.filter((device) => device.enabled && !device.unavailable),
    [devices]
  );

  // A device saved on the profile may since have been disabled/removed; drop it so the select does
  // not silently keep an unselectable value.
  useEffect(() => {
    if (
      deviceId &&
      !selectableDevices.some((device) => device.deviceId === deviceId)
    ) {
      setDeviceId('');
    }
  }, [deviceId, selectableDevices]);

  const handleNameChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => setName(event.target.value),
    []
  );

  const handleModeChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) => {
      setMode(event.target.value);
      setValidationError(null);
    },
    []
  );

  const handleCodecChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setCodec(event.target.value),
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

  const handlePresetChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setPreset(event.target.value),
    []
  );

  const handleMaxHeightChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setMaxHeight(event.target.value),
    []
  );

  const handleTagChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => setTag(event.target.value),
    []
  );

  const handlePreferEnglishAudioChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setPreferEnglishAudio(event.target.checked),
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

  const handleDeviceChange = useCallback(
    (event: React.ChangeEvent<HTMLSelectElement>) =>
      setDeviceId(event.target.value),
    []
  );

  const handleDefaultChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) =>
      setIsDefault(event.target.checked),
    []
  );

  const handleSavePress = useCallback(() => {
    if (mode === 'targetSize' && !targetSize) {
      setValidationError(translate('CompressionTargetSizeRequired'));

      return;
    }

    if (mode === 'percentageReduction' && !targetPercent) {
      setValidationError(translate('CompressionTargetPercentRequired'));

      return;
    }

    setValidationError(null);

    onSave({
      id: profile?.id ?? 0,
      name: name.trim() || translate('Untitled'),
      codec: mode === 'remux' ? null : codec,
      mode,
      qualityValue: quality ? Number(quality) : 0,
      preset: preset || null,
      targetSizeMB:
        mode === 'targetSize' && targetSize ? Number(targetSize) : null,
      targetPercent:
        mode === 'percentageReduction' && targetPercent
          ? Number(targetPercent)
          : null,
      maxHeight: mode === 'remux' || !maxHeight ? null : Number(maxHeight),
      tag: tag.trim() || null,
      preferEnglishAudio,
      deviceId: mode === 'remux' || !deviceId ? null : deviceId,
      container: mode === 'remux' ? container : null,
      isDefault,
    });
  }, [
    profile,
    name,
    mode,
    codec,
    container,
    quality,
    preset,
    targetSize,
    targetPercent,
    maxHeight,
    tag,
    preferEnglishAudio,
    deviceId,
    isDefault,
    onSave,
  ]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate(profile ? 'EditTranscodeProfile' : 'AddTranscodeProfile')}
      </ModalHeader>

      <ModalBody>
        {error ? <Alert kind={kinds.DANGER}>{error}</Alert> : null}

        <div className={ROW_CLASS}>
          <label
            htmlFor={`${formId}-name`}
            className="mb-[5px] block font-bold"
          >
            {translate('Name')}
          </label>

          <input
            id={`${formId}-name`}
            className={SELECT_CLASS}
            type="text"
            value={name}
            onChange={handleNameChange}
          />
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

        <div className={ROW_CLASS}>
          <label htmlFor={`${formId}-tag`} className="mb-[5px] block font-bold">
            {translate('TranscodeTag')}
          </label>

          <input
            id={`${formId}-tag`}
            className={SELECT_CLASS}
            type="text"
            placeholder={translate('TranscodeTagPlaceholder')}
            value={tag}
            onChange={handleTagChange}
          />

          <div className={`mt-[5px] ${META_CLASS}`}>
            {translate('TranscodeTagHelpText')}
          </div>
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
                htmlFor={`${formId}-codec`}
                className="mb-[5px] block font-bold"
              >
                {translate('VideoCodec')}
              </label>

              <select
                id={`${formId}-codec`}
                className={SELECT_CLASS}
                value={codec}
                onChange={handleCodecChange}
              >
                {CODECS.map((item) => (
                  <option key={item} value={item}>
                    {item.toUpperCase()}
                  </option>
                ))}
              </select>
            </div>

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
                <option value="">{translate('KeepSourceResolution')}</option>

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
                  value={targetSize}
                  onChange={handleTargetSizeChange}
                />

                {validationError ? (
                  <div className="mt-[5px] text-[var(--alertDangerColor)]">
                    {validationError}
                  </div>
                ) : null}
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
                  value={targetPercent}
                  onChange={handleTargetPercentChange}
                />

                {validationError ? (
                  <div className="mt-[5px] text-[var(--alertDangerColor)]">
                    {validationError}
                  </div>
                ) : null}
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
                  placeholder={String(settings?.defaultQualityValue ?? 23)}
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
                value={preset}
                onChange={handlePresetChange}
              />
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
                <option value="">{translate('AnyDevice')}</option>

                {selectableDevices.map((device) => (
                  <option key={device.deviceId} value={device.deviceId}>
                    {getDeviceLabel(device)}
                  </option>
                ))}
              </select>
            </div>
          </>
        )}

        <label className="flex items-center gap-[5px]">
          <input
            type="checkbox"
            checked={preferEnglishAudio}
            onChange={handlePreferEnglishAudioChange}
          />

          {translate('PreferEnglishAudio')}
        </label>

        <label className="mt-[8px] flex items-center gap-[5px]">
          <input
            type="checkbox"
            checked={isDefault}
            onChange={handleDefaultChange}
          />

          {translate('DefaultProfile')}
        </label>
      </ModalBody>

      <ModalFooter>
        <SpinnerButton
          isSpinning={false}
          isDisabled={isSaving}
          onPress={onModalClose}
        >
          {translate('Cancel')}
        </SpinnerButton>

        <SpinnerButton
          kind={kinds.PRIMARY}
          isSpinning={isSaving}
          onPress={handleSavePress}
        >
          {translate('Save')}
        </SpinnerButton>
      </ModalFooter>
    </ModalContent>
  );
}

export default TranscodeProfileModalContent;
