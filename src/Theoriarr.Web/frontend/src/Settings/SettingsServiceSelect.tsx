import React from 'react';
import ServiceSelect from 'Components/ServiceSelect/ServiceSelect';
import { useSettingsServiceControls } from './SettingsServiceContext';

// Rendered by SettingsToolbar only when the surrounding page opted into a
// per-domain provider, so shared/series-only pages do not show a dead switch.
function SettingsServiceSelect() {
  const { service, setService } = useSettingsServiceControls();

  if (!setService) {
    return null;
  }

  return (
    <ServiceSelect value={service} label="Configure" onChange={setService} />
  );
}

export default SettingsServiceSelect;
