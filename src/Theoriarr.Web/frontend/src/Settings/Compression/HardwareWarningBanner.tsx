import React from 'react';
import Alert from 'Components/Alert';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { TranscodeDevice } from './useMediaCompressionCapabilities';

interface HardwareWarningBannerProps {
  devices: TranscodeDevice[];
}

function HardwareWarningBanner({ devices }: HardwareWarningBannerProps) {
  const hasEnabledHardware = devices.some(
    (device) => device.kind !== 'Software' && device.enabled
  );

  if (!hasEnabledHardware) {
    return (
      <Alert kind={kinds.INFO}>
        {translate('HardwareTranscodingDisabledNote')}
      </Alert>
    );
  }

  return (
    <Alert kind={kinds.WARNING}>
      <div className="font-bold">
        {translate('HardwareTranscodingWarningTitle')}
      </div>

      <div>{translate('HardwareTranscodingWarning')}</div>
    </Alert>
  );
}

export default HardwareWarningBanner;
