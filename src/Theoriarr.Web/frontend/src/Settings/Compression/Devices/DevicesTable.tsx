import React, { useCallback, useEffect, useState } from 'react';
import SpinnerButton from 'Components/Link/SpinnerButton';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { TranscodeDevice } from '../useMediaCompressionCapabilities';

const ROW_CLASS = 'mb-[10px] rounded-[4px] border border-[#e5e5e5] p-[10px]';
const META_CLASS = 'text-[var(--helpTextColor)]';
const INPUT_CLASS =
  'w-[80px] rounded-[4px] border border-[#e5e5e5] bg-[var(--inputBackgroundColor)] px-[6px] py-[4px] text-[var(--textColor)]';

interface DevicesTableProps {
  devices: TranscodeDevice[];
  isSaving: boolean;
  onSave(devices: TranscodeDevice[]): void;
}

function DevicesTable({ devices, isSaving, onSave }: DevicesTableProps) {
  const [draft, setDraft] = useState(devices);

  useEffect(() => {
    setDraft(devices);
  }, [devices]);

  const update = useCallback(
    (deviceId: string, patch: Partial<TranscodeDevice>) => {
      setDraft((current) =>
        current.map((device) =>
          device.deviceId === deviceId ? { ...device, ...patch } : device
        )
      );
    },
    []
  );

  const handleSave = useCallback(() => onSave(draft), [onSave, draft]);

  const handleDisableAllHardware = useCallback(() => {
    setDraft((current) =>
      current.map((device) =>
        device.kind === 'Software' ? device : { ...device, enabled: false }
      )
    );
  }, []);

  const handleEnabledChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      update(event.target.dataset.deviceId!, { enabled: event.target.checked });
    },
    [update]
  );

  const handleCodecChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      const { deviceId, codec } = event.target.dataset;
      const enabled = event.target.checked;

      setDraft((current) =>
        current.map((device) =>
          device.deviceId === deviceId
            ? {
                ...device,
                codecs: (device.codecs ?? []).map((setting) =>
                  setting.codec === codec ? { ...setting, enabled } : setting
                ),
              }
            : device
        )
      );
    },
    []
  );

  const handleMaxParallelChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      update(event.target.dataset.deviceId!, {
        maxParallel: Number(event.target.value),
      });
    },
    [update]
  );

  const handlePriorityChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      update(event.target.dataset.deviceId!, {
        priority: Number(event.target.value),
      });
    },
    [update]
  );

  const handleWeightChange = useCallback(
    (event: React.ChangeEvent<HTMLInputElement>) => {
      update(event.target.dataset.deviceId!, {
        weight: Number(event.target.value),
      });
    },
    [update]
  );

  return (
    <div>
      <div className="mb-[10px] flex gap-[10px]">
        <SpinnerButton
          kind={kinds.PRIMARY}
          isSpinning={isSaving}
          onPress={handleSave}
        >
          {translate('SaveDevices')}
        </SpinnerButton>

        <SpinnerButton
          kind={kinds.WARNING}
          isSpinning={false}
          onPress={handleDisableAllHardware}
        >
          {translate('DisableAllHardwareDevices')}
        </SpinnerButton>
      </div>

      {draft.map((device) => (
        <div key={device.deviceId} className={ROW_CLASS}>
          <div className="flex flex-wrap items-center gap-[10px]">
            <input
              type="checkbox"
              data-device-id={device.deviceId}
              aria-label={translate('EnableTranscodeDevice', {
                name: device.name,
              })}
              checked={device.enabled}
              onChange={handleEnabledChange}
            />

            <span className="font-semibold">{device.name}</span>
            <span className={META_CLASS}>{device.kind}</span>
          </div>

          <div className={`mt-[5px] ${META_CLASS}`}>
            {device.capabilities?.encoders?.join(', ') || '—'}
          </div>

          <div className="mt-[5px] flex flex-wrap items-center gap-[15px]">
            <span className={META_CLASS}>{translate('Codecs')}:</span>

            {(device.codecs ?? []).map((setting) => (
              <label
                key={setting.codec}
                className="flex items-center gap-[5px]"
                title={
                  setting.supported
                    ? setting.encoder ?? undefined
                    : translate('CodecNotSupported')
                }
              >
                <input
                  type="checkbox"
                  data-device-id={device.deviceId}
                  data-codec={setting.codec}
                  checked={setting.enabled}
                  onChange={handleCodecChange}
                />

                <span className={setting.supported ? '' : META_CLASS}>
                  {setting.codec.toUpperCase()}
                </span>
              </label>
            ))}
          </div>

          <div className="mt-[5px] flex flex-wrap items-center gap-[15px]">
            <label className="flex items-center gap-[5px]">
              {translate('MaxParallel')}
              <input
                className={INPUT_CLASS}
                type="number"
                min={1}
                data-device-id={device.deviceId}
                value={device.maxParallel}
                onChange={handleMaxParallelChange}
              />
            </label>

            <label className="flex items-center gap-[5px]">
              {translate('Priority')}
              <input
                className={INPUT_CLASS}
                type="number"
                min={0}
                data-device-id={device.deviceId}
                value={device.priority}
                onChange={handlePriorityChange}
              />
            </label>

            <label className="flex items-center gap-[5px]">
              {translate('Weight')}
              <input
                className={INPUT_CLASS}
                type="number"
                min={0}
                data-device-id={device.deviceId}
                value={device.weight}
                onChange={handleWeightChange}
              />
            </label>
          </div>
        </div>
      ))}
    </div>
  );
}

export default DevicesTable;
