import moment from 'moment-timezone';
import React, { useCallback, useEffect, useState } from 'react';
import { saveDimensions, useAppValue } from 'App/appStore';
import AppUpdatedModal from 'App/AppUpdatedModal';
import ColorImpairedContext from 'App/ColorImpairedContext';
import ConnectionLostModal from 'App/ConnectionLostModal';
import SignalRListener from 'Components/SignalRListener';
import AuthenticationRequiredModal from 'FirstRun/AuthenticationRequiredModal';
import useAppPage from 'Helpers/Hooks/useAppPage';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { useSystemStatusData } from 'System/Status/useSystemStatus';
import ErrorPage from './ErrorPage';
import PageHeader from './Header/PageHeader';
import LoadingPage from './LoadingPage';
import PageSidebar from './Sidebar/PageSidebar';

interface PageProps {
  children: React.ReactNode;
}

function Page({ children }: PageProps) {
  const isUpdated = useAppValue('isUpdated');
  const isDisconnected = useAppValue('isDisconnected');
  const version = useAppValue('version');
  const { hasError, errors, isPopulated, isLocalStorageSupported } =
    useAppPage();
  const [isUpdatedModalOpen, setIsUpdatedModalOpen] = useState(false);
  const [isConnectionLostModalOpen, setIsConnectionLostModalOpen] =
    useState(false);

  const { enableColorImpairedMode, timeZone } = useUiSettingsValues();
  const { authentication, authenticationDisabled } = useSystemStatusData();

  // The forced-authentication nag is only shown when authentication is off by
  // configuration; an explicit operator override (THEORIARR_DISABLE_AUTH) is
  // intentional and must not block the UI with an unsavable modal.
  const showAuthenticationRequiredModal =
    authentication === 'none' && !authenticationDisabled;

  const handleUpdatedModalClose = useCallback(() => {
    setIsUpdatedModalOpen(false);
  }, []);

  const handleResize = useCallback(() => {
    saveDimensions({
      width: window.innerWidth,
      height: window.innerHeight,
    });
  }, []);

  useEffect(() => {
    window.addEventListener('resize', handleResize);

    return () => {
      window.removeEventListener('resize', handleResize);
    };
  }, [handleResize]);

  useEffect(() => {
    if (isDisconnected) {
      setIsConnectionLostModalOpen(true);
    }
  }, [isDisconnected]);

  useEffect(() => {
    if (isUpdated) {
      setIsUpdatedModalOpen(true);
    }
  }, [isUpdated]);

  useEffect(() => {
    try {
      moment.tz.setDefault(timeZone);
    } catch (error) {
      console.error(
        `Error converting to timezone ${timeZone}. Using system timezone.`,
        error
      );
    }
  }, [timeZone]);

  if (hasError || !isLocalStorageSupported) {
    return (
      <ErrorPage
        {...errors}
        version={version}
        isLocalStorageSupported={isLocalStorageSupported}
      />
    );
  }

  if (!isPopulated) {
    return <LoadingPage />;
  }

  return (
    <ColorImpairedContext.Provider value={enableColorImpairedMode}>
      <div className="flex h-full flex-col bg-base-100 text-base-content max-md:h-auto max-md:grow">
        <SignalRListener />

        <PageHeader />

        <div className="relative flex min-h-0 flex-1">
          <PageSidebar />

          {children}
        </div>

        <AppUpdatedModal
          isOpen={isUpdatedModalOpen}
          onModalClose={handleUpdatedModalClose}
        />

        <ConnectionLostModal isOpen={isConnectionLostModalOpen} />

        <AuthenticationRequiredModal isOpen={showAuthenticationRequiredModal} />
      </div>
    </ColorImpairedContext.Provider>
  );
}

export default Page;
