import React from 'react';
import Link from 'Components/Link/Link';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import translate from 'Utilities/String/translate';
import SettingsToolbar from './SettingsToolbar';

const SUMMARY_CLASS = 'mt-[10px] mb-[30px] text-[var(--helpTextColor)]';

function Settings() {
  return (
    <PageContent title={translate('Settings')}>
      <SettingsToolbar hasPendingChanges={false} />

      <PageContentBody>
        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/mediamanagement"
        >
          {translate('MediaManagement')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('MediaManagementSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/transcode"
        >
          {translate('Transcode')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('CompressionSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/profiles"
        >
          {translate('Profiles')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('ProfilesSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/quality"
        >
          {translate('Quality')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('QualitySettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/customformats"
        >
          {translate('CustomFormats')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('CustomFormatsSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/indexers"
        >
          {translate('Indexers')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('IndexersSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/downloadclients"
        >
          {translate('DownloadClients')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('DownloadClientsSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/importlists"
        >
          {translate('ImportLists')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('ImportListsSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/connect"
        >
          {translate('Connect')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('ConnectSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/metadata"
        >
          {translate('Metadata')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('MetadataSettingsSeriesSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/metadatasource"
        >
          {translate('MetadataSource')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('MetadataSourceSettingsSeriesSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/tags"
        >
          {translate('Tags')}
        </Link>

        <div className={SUMMARY_CLASS}>{translate('TagsSettingsSummary')}</div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/general"
        >
          {translate('General')}
        </Link>

        <div className={SUMMARY_CLASS}>
          {translate('GeneralSettingsSummary')}
        </div>

        <Link
          className="border-b border-[#e5e5e5] text-[21px] text-[var(--textColor)] hover:text-[#616573]! hover:no-underline!"
          to="/settings/ui"
        >
          {translate('Ui')}
        </Link>

        <div className={SUMMARY_CLASS}>{translate('UiSettingsSummary')}</div>
      </PageContentBody>
    </PageContent>
  );
}

export default Settings;
