import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import { useAppValue } from 'App/appStore';
import CommandNames from 'Commands/CommandNames';
import { useCommandExecuting, useExecuteCommand } from 'Commands/useCommands';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator, {
  LOADING_INDICATOR_CLASS,
} from 'Components/Loading/LoadingIndicator';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import {
  UpdateMechanism,
  useGeneralSettings,
} from 'Settings/General/useGeneralSettings';
import useUpdateSettings from 'Settings/General/useUpdateSettings';
import { useUiSettingsValues } from 'Settings/UI/useUiSettings';
import { useSystemStatusData } from 'System/Status/useSystemStatus';
import formatDate from 'Utilities/Date/formatDate';
import formatDateTime from 'Utilities/Date/formatDateTime';
import translate from 'Utilities/String/translate';
import UpdateChanges from './UpdateChanges';
import useUpdates from './useUpdates';

const MESSAGE_CLASS = 'pl-[5px] text-[18px] leading-[30px]';

const VERSION_REGEX = /\d+\.\d+\.\d+\.\d+/i;

function Updates() {
  const currentVersion = useAppValue('version');
  const { packageUpdateMechanismMessage } = useSystemStatusData();

  const { shortDateFormat, longDateFormat, timeFormat } = useUiSettingsValues();
  const isInstallingUpdate = useCommandExecuting(
    CommandNames.ApplicationUpdate
  );

  const {
    data: updates,
    isFetched: isUpdatesFetched,
    isLoading: isLoadingUpdates,
    error: updatesError,
  } = useUpdates();
  const {
    data: updateSettings,
    isFetched: isSettingsFetched,
    isLoading: isLoadingSettings,
    error: settingsError,
  } = useUpdateSettings();

  const executeCommand = useExecuteCommand();
  const [isMajorUpdateModalOpen, setIsMajorUpdateModalOpen] = useState(false);
  const isFetching = isLoadingUpdates || isLoadingSettings;
  const isPopulated = isUpdatesFetched && isSettingsFetched;
  const updateMechanism = updateSettings?.updateMechanism ?? 'builtIn';
  const hasError = !!(updatesError || settingsError);
  const hasUpdates = isPopulated && !hasError && updates.length > 0;
  const noUpdates = isPopulated && !hasError && !updates.length;

  const externalUpdaterPrefix = translate('UpdateAppDirectlyLoadError');
  const externalUpdaterMessages: Partial<Record<UpdateMechanism, string>> = {
    external: translate('ExternalUpdater'),
    apt: translate('AptUpdater'),
    docker: translate('DockerUpdater'),
  };

  const { isMajorUpdate, hasUpdateToInstall } = useMemo(() => {
    const majorVersion = parseInt(
      currentVersion.match(VERSION_REGEX)?.[0] ?? '0'
    );

    const latestVersion = updates[0]?.version;
    const latestMajorVersion = parseInt(
      latestVersion?.match(VERSION_REGEX)?.[0] ?? '0'
    );

    return {
      isMajorUpdate: latestMajorVersion > majorVersion,
      hasUpdateToInstall: updates.some(
        (update) => update.installable && update.latest
      ),
    };
  }, [currentVersion, updates]);

  const noUpdateToInstall = hasUpdates && !hasUpdateToInstall;

  const handleInstallLatestPress = useCallback(() => {
    if (isMajorUpdate) {
      setIsMajorUpdateModalOpen(true);
    } else {
      executeCommand({ name: CommandNames.ApplicationUpdate });
    }
  }, [isMajorUpdate, setIsMajorUpdateModalOpen, executeCommand]);

  const handleInstallLatestMajorVersionPress = useCallback(() => {
    setIsMajorUpdateModalOpen(false);

    executeCommand({
      name: CommandNames.ApplicationUpdate,
      installMajorUpdate: true,
    });
  }, [setIsMajorUpdateModalOpen, executeCommand]);

  const handleCancelMajorVersionPress = useCallback(() => {
    setIsMajorUpdateModalOpen(false);
  }, [setIsMajorUpdateModalOpen]);

  useGeneralSettings();

  return (
    <PageContent title={translate('Updates')}>
      <PageContentBody>
        {isPopulated || hasError ? null : <LoadingIndicator />}

        {noUpdates ? (
          <Alert kind={kinds.INFO}>{translate('NoUpdatesAreAvailable')}</Alert>
        ) : null}

        {hasUpdateToInstall ? (
          <div className="flex mb-[20px]">
            {updateMechanism === 'builtIn' || updateMechanism === 'script' ? (
              <SpinnerButton
                kind={kinds.PRIMARY}
                isSpinning={isInstallingUpdate}
                onPress={handleInstallLatestPress}
              >
                {translate('InstallLatest')}
              </SpinnerButton>
            ) : (
              <>
                <Icon name={icons.WARNING} kind={kinds.WARNING} size={30} />

                <div className={MESSAGE_CLASS}>
                  {externalUpdaterPrefix}{' '}
                  <InlineMarkdown
                    data={
                      packageUpdateMechanismMessage ||
                      externalUpdaterMessages[updateMechanism] ||
                      externalUpdaterMessages.external
                    }
                  />
                </div>
              </>
            )}

            {isFetching ? (
              <LoadingIndicator
                className={classNames(
                  LOADING_INDICATOR_CLASS,
                  'mt-[5px] ml-auto'
                )}
                size={20}
              />
            ) : null}
          </div>
        ) : null}

        {noUpdateToInstall && (
          <div className="flex mb-[20px]">
            <Icon
              className="text-[#37bc9b] text-[30px]"
              name={icons.CHECK_CIRCLE}
              size={30}
            />
            <div className={MESSAGE_CLASS}>{translate('OnLatestVersion')}</div>

            {isFetching && (
              <LoadingIndicator
                className={classNames(
                  LOADING_INDICATOR_CLASS,
                  'mt-[5px] ml-auto'
                )}
                size={20}
              />
            )}
          </div>
        )}

        {hasUpdates && (
          <div>
            {updates.map((update) => {
              return (
                <div key={update.version} className="mt-[20px]">
                  <div className="flex items-center mb-[10px] pb-[5px] border-b border-solid border-b-[#e5e5e5]">
                    <div className="text-[21px]">{update.version}</div>
                    <div className="px-[5px]">&mdash;</div>
                    <div
                      className="text-[16px]"
                      title={formatDateTime(
                        update.releaseDate,
                        longDateFormat,
                        timeFormat
                      )}
                    >
                      {formatDate(update.releaseDate, shortDateFormat)}
                    </div>

                    {update.branch === 'main' ? null : (
                      <Label className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default ml-[10px] text-[14px]">
                        {update.branch}
                      </Label>
                    )}

                    {update.version === currentVersion ? (
                      <Label
                        className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default ml-[10px] text-[14px]"
                        kind={kinds.SUCCESS}
                        title={formatDateTime(
                          update.installedOn,
                          longDateFormat,
                          timeFormat
                        )}
                      >
                        {translate('CurrentlyInstalled')}
                      </Label>
                    ) : null}

                    {update.version !== currentVersion && update.installedOn ? (
                      <Label
                        className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default ml-[10px] text-[14px]"
                        kind={kinds.INVERSE}
                        title={formatDateTime(
                          update.installedOn,
                          longDateFormat,
                          timeFormat
                        )}
                      >
                        {translate('PreviouslyInstalled')}
                      </Label>
                    ) : null}
                  </div>

                  {update.changes ? (
                    <div>
                      <UpdateChanges
                        title={translate('New')}
                        changes={update.changes.new}
                      />

                      <UpdateChanges
                        title={translate('Fixed')}
                        changes={update.changes.fixed}
                      />
                    </div>
                  ) : (
                    <div>{translate('MaintenanceRelease')}</div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        {updatesError ? (
          <Alert kind={kinds.WARNING}>
            {translate('FailedToFetchUpdates')}
          </Alert>
        ) : null}

        {settingsError ? (
          <Alert kind={kinds.DANGER}>
            {translate('FailedToFetchSettings')}
          </Alert>
        ) : null}

        <ConfirmModal
          isOpen={isMajorUpdateModalOpen}
          kind={kinds.WARNING}
          title={translate('InstallMajorVersionUpdate')}
          message={
            <div>
              <div>{translate('InstallMajorVersionUpdateMessage')}</div>
              <div>
                <InlineMarkdown
                  data={translate('InstallMajorVersionUpdateMessageLink', {
                    domain: 'github.com',
                    url: 'https://github.com/MagicBOTAlex/Theoriarr/releases',
                  })}
                />
              </div>
            </div>
          }
          confirmLabel={translate('Install')}
          onConfirm={handleInstallLatestMajorVersionPress}
          onCancel={handleCancelMajorVersionPress}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default Updates;
