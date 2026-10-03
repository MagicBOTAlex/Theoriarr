import React, { createContext, useContext, useMemo, useState } from 'react';
import { ServiceId } from 'Services';

// The Settings pages are shared between the two subsystems. A page that can be
// configured per-domain wraps its content in `SettingsServiceProvider`; the
// toolbar then renders a Series/Movies switch and every settings hook below
// fetches from the selected service (movies or series). Pages without a
// provider fall back to `series`, preserving the series settings behaviour.
interface SettingsServiceContextValue {
  service: ServiceId;
  setService?: (service: ServiceId) => void;
}

const SettingsServiceContext = createContext<SettingsServiceContextValue>({
  service: 'series',
});

interface SettingsServiceProviderProps {
  service?: ServiceId;
  children: React.ReactNode;
}

export function SettingsServiceProvider({
  service: initialService = 'series',
  children,
}: SettingsServiceProviderProps) {
  const [service, setService] = useState<ServiceId>(initialService);
  const value = useMemo(() => ({ service, setService }), [service]);

  return (
    <SettingsServiceContext.Provider value={value}>
      {children}
    </SettingsServiceContext.Provider>
  );
}

export function useSettingsService(): ServiceId {
  return useContext(SettingsServiceContext).service;
}

export function useSettingsServiceControls(): SettingsServiceContextValue {
  return useContext(SettingsServiceContext);
}
