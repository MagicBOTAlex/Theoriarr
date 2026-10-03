import React, { useCallback, useEffect, useState } from 'react';
import { showMessage } from 'App/messagesStore';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import Form from 'Components/Form/Form';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import { icons, inputTypes, kinds, sizes } from 'Helpers/Props';
import { ERROR } from 'Helpers/Props/messageTypes';
import {
  TranscodeJob,
  useBlockedTranscodeJobs,
  useForceStopTranscodeJobs,
} from 'Library/Compression/useTranscodeJobs';
import SettingsToolbar from 'Settings/SettingsToolbar';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import DevicesTable from './Devices/DevicesTable';
import HardwareWarningBanner from './HardwareWarningBanner';
import ProfilesTable from './Profiles/ProfilesTable';
import {
  CompressionSettings,
  useCompressionSettings,
  useUpdateCompressionSettings,
} from './useCompressionSettings';
import {
  TranscodeDevice,
  useMediaCompressionCapabilities,
  useReprobeMediaCompression,
  useUpdateTranscodeDevices,
} from './useMediaCompressionCapabilities';

const DEVICES_MESSAGE_ID = -1012;
const REPROBE_MESSAGE_ID = -1013;

const MODE_OPTIONS: EnhancedSelectInputValue<string>[] = [
  {
    key: 'quality',
    get value() {
      return translate('CompressionModeQuality');
    },
  },
  {
    key: 'targetSize',
    get value() {
      return translate('CompressionModeTargetSize');
    },
  },
  {
    key: 'percentageReduction',
    get value() {
      return translate('CompressionModePercentage');
    },
  },
  {
    key: 'remux',
    get value() {
      return translate('CompressionModeRemux');
    },
  },
];

const CODEC_OPTIONS: EnhancedSelectInputValue<string>[] = [
  { key: 'hevc', value: 'HEVC' },
  { key: 'h264', value: 'H.264' },
  { key: 'av1', value: 'AV1' },
];

const REVIEW_ACTION_OPTIONS: EnhancedSelectInputValue<string>[] = [
  {
    key: 'overwrite',
    get value() {
      return translate('OverwriteOriginal');
    },
  },
  {
    key: 'keepBoth',
    get value() {
      return translate('KeepBoth');
    },
  },
  {
    key: 'discard',
    get value() {
      return translate('Discard');
    },
  },
];

function Compression() {
  const { isFetching, isFetched, error, capabilities } =
    useMediaCompressionCapabilities();
  const { reprobe, isReprobing, reprobeError } = useReprobeMediaCompression();
  const { updateDevices, isUpdating } = useUpdateTranscodeDevices();
  const { settings } = useCompressionSettings();
  const {
    updateSettings,
    isUpdating: isUpdatingSettings,
    updateError: settingsUpdateError,
  } = useUpdateCompressionSettings();
  const { refetchBlockedJobs } = useBlockedTranscodeJobs();
  const { forceStopJobs, isForceStopping, forceStopError } =
    useForceStopTranscodeJobs();

  const [blockedJobs, setBlockedJobs] = useState<TranscodeJob[]>([]);

  useEffect(() => {
    if (reprobeError) {
      showMessage({
        id: REPROBE_MESSAGE_ID,
        name: translate('Transcoding'),
        message: translate('TranscodeReprobeFailed'),
        type: ERROR,
        hideAfter: 10,
      });
    }
  }, [reprobeError]);

  const handleReprobe = useCallback(() => {
    reprobe({ force: true });
  }, [reprobe]);

  const handleSaveDevices = useCallback(
    (devices: TranscodeDevice[]) => {
      updateDevices(devices, {
        onSuccess: async () => {
          const result = await refetchBlockedJobs();
          const running = result.data ?? [];

          if (running.length) {
            setBlockedJobs([...running]);
          }
        },
        onError: () => {
          showMessage({
            id: DEVICES_MESSAGE_ID,
            name: translate('Transcoding'),
            message: translate('TranscodeDevicesSaveFailed'),
            type: ERROR,
            hideAfter: 10,
          });
        },
      });
    },
    [updateDevices, refetchBlockedJobs]
  );

  const handleForceStopPress = useCallback(() => {
    forceStopJobs(
      { jobIds: blockedJobs.map((job) => job.id) },
      { onSuccess: () => setBlockedJobs([]) }
    );
  }, [forceStopJobs, blockedJobs]);

  // Keep the running jobs: the settings are already saved, so they apply to new jobs while the
  // overflow jobs finish on their current settings.
  const handleKeepRunning = useCallback(() => setBlockedJobs([]), []);

  const handleSettingChange = useCallback(
    ({ name, value }: InputChanged) => {
      if (!settings) {
        return;
      }

      updateSettings({ ...settings, [name]: value } as CompressionSettings);
    },
    [settings, updateSettings]
  );

  const devices = capabilities.devices ?? [];

  const reprobeButton = (
    <PageToolbarButton
      label={translate('ReprobeDevices')}
      iconName={icons.REFRESH}
      isSpinning={isReprobing}
      onPress={handleReprobe}
    />
  );

  return (
    <PageContent title={translate('Transcode')}>
      <SettingsToolbar showSave={false} additionalButtons={reprobeButton} />

      <PageContentBody>
        {isFetching && !isFetched ? <LoadingIndicator /> : null}

        {error ? (
          <Alert kind={kinds.DANGER}>
            {translate('CompressionCapabilitiesLoadError')}
          </Alert>
        ) : null}

        {settingsUpdateError ? (
          <Alert kind={kinds.DANGER}>
            {translate('CompressionSettingsSaveError')}
          </Alert>
        ) : null}

        {isFetched && !error ? (
          <>
            <HardwareWarningBanner devices={devices} />

            <FieldSet legend={translate('TranscodeProfiles')}>
              <ProfilesTable devices={devices} />
            </FieldSet>

            {settings ? (
              <>
                <FieldSet legend={translate('CompressionSettings')}>
                  <Form>
                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>
                        {translate('MediaCompressionEnabled')}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="mediaCompressionEnabled"
                        helpText={translate('MediaCompressionEnabledHelp')}
                        isDisabled={isUpdatingSettings}
                        value={settings.mediaCompressionEnabled}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('TranscodeTempFolder')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="transcodeTempFolder"
                        helpText={translate('TranscodeTempFolderHelp')}
                        readOnly={isUpdatingSettings}
                        value={settings.transcodeTempFolder ?? ''}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('MaxConcurrentJobs')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="maxConcurrentJobs"
                        min={1}
                        helpText={translate('MaxConcurrentJobsHelp')}
                        readOnly={isUpdatingSettings}
                        value={settings.maxConcurrentJobs}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('PreferHardware')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.CHECK}
                        name="preferHardware"
                        helpText={translate('PreferHardwareHelp')}
                        isDisabled={isUpdatingSettings}
                        value={settings.preferHardware}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('TranscodeNice')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="transcodeNice"
                        min={0}
                        helpText={translate('TranscodeNiceHelp')}
                        readOnly={isUpdatingSettings}
                        value={settings.transcodeNice}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>
                  </Form>
                </FieldSet>

                <FieldSet legend={translate('CompressionDefaults')}>
                  <Form>
                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('DefaultVideoCodec')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="defaultVideoCodec"
                        values={CODEC_OPTIONS}
                        isDisabled={isUpdatingSettings}
                        value={settings.defaultVideoCodec ?? 'hevc'}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>
                        {translate('DefaultRateControlMode')}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="defaultRateControlMode"
                        values={MODE_OPTIONS}
                        helpText={translate('DefaultRateControlModeHelp')}
                        isDisabled={isUpdatingSettings}
                        value={settings.defaultRateControlMode}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>
                        {translate('DefaultTargetEpisodeSizeMB')}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="defaultTargetEpisodeSizeMB"
                        min={1}
                        unit="MB"
                        readOnly={isUpdatingSettings}
                        value={settings.defaultTargetEpisodeSizeMB}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>
                        {translate('DefaultTargetMovieSizeMB')}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="defaultTargetMovieSizeMB"
                        min={1}
                        unit="MB"
                        readOnly={isUpdatingSettings}
                        value={settings.defaultTargetMovieSizeMB}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('DefaultQualityValue')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="defaultQualityValue"
                        min={0}
                        max={63}
                        readOnly={isUpdatingSettings}
                        value={settings.defaultQualityValue}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('DefaultPreset')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.TEXT}
                        name="defaultPreset"
                        readOnly={isUpdatingSettings}
                        value={settings.defaultPreset ?? ''}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>{translate('DefaultReducePercent')}</FormLabel>

                      <FormInputGroup
                        type={inputTypes.NUMBER}
                        name="defaultReducePercent"
                        min={1}
                        max={99}
                        unit="%"
                        readOnly={isUpdatingSettings}
                        value={settings.defaultReducePercent}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>

                    <FormGroup size={sizes.MEDIUM}>
                      <FormLabel>
                        {translate('TranscodeReviewDefault')}
                      </FormLabel>

                      <FormInputGroup
                        type={inputTypes.SELECT}
                        name="transcodeReviewDefault"
                        values={REVIEW_ACTION_OPTIONS}
                        helpText={translate('TranscodeReviewDefaultHelp')}
                        isDisabled={isUpdatingSettings}
                        value={settings.transcodeReviewDefault}
                        onChange={handleSettingChange}
                      />
                    </FormGroup>
                  </Form>
                </FieldSet>
              </>
            ) : null}

            <FieldSet legend={translate('TranscodeDevices')}>
              {settings ? (
                <Form>
                  <FormGroup size={sizes.MEDIUM}>
                    <FormLabel>
                      {translate('DelegateEasiestJobsFirst')}
                    </FormLabel>

                    <FormInputGroup
                      type={inputTypes.CHECK}
                      name="transcodeEasiestJobsFirst"
                      helpText={translate('DelegateEasiestJobsFirstHelp')}
                      isDisabled={isUpdatingSettings}
                      value={settings.transcodeEasiestJobsFirst}
                      onChange={handleSettingChange}
                    />
                  </FormGroup>
                </Form>
              ) : null}

              <DevicesTable
                devices={devices}
                isSaving={isUpdating}
                onSave={handleSaveDevices}
              />
            </FieldSet>

            <FieldSet legend={translate('FFmpeg')}>
              <div className="text-[var(--helpTextColor)]">
                {translate('Path')}: {capabilities.ffmpegPath || '—'}
              </div>

              <div className="text-[var(--helpTextColor)]">
                {translate('Version')}: {capabilities.ffmpegVersion || '—'}
              </div>

              <div className="text-[var(--helpTextColor)]">
                {translate('Fingerprint')}: {capabilities.fingerprint || '—'}
              </div>
            </FieldSet>
          </>
        ) : null}
      </PageContentBody>

      <ConfirmModal
        isOpen={blockedJobs.length > 0}
        kind={kinds.WARNING}
        title={translate('ForceStopJobs')}
        message={
          <>
            <div>
              {translate('ForceStopBlockedJobsMessage', {
                count: blockedJobs.length,
              })}
            </div>

            <div className="mt-[10px] text-[var(--helpTextColor)]">
              {translate('KeepRunningJobsHint')}
            </div>

            {forceStopError ? (
              <Alert kind={kinds.DANGER}>
                {translate('TranscodeForceStopFailed')}
              </Alert>
            ) : null}
          </>
        }
        confirmLabel={translate('ForceStop')}
        cancelLabel={translate('KeepRunningJobs')}
        isSpinning={isForceStopping}
        onConfirm={handleForceStopPress}
        onCancel={handleKeepRunning}
      />
    </PageContent>
  );
}

export default Compression;
