import classNames from 'classnames';
import React, { useEffect } from 'react';
import DescriptionList, {
  DESCRIPTION_LIST_CLASS,
} from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import FieldSet from 'Components/FieldSet';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import useSystemStatus from '../useSystemStatus';
import StartTime from './StartTime';

function About() {
  const { data, refetch } = useSystemStatus();

  const {
    version,
    packageVersion,
    packageAuthor,
    isNetCore,
    isContainerized,
    runtimeVersion,
    databaseVersion,
    databaseType,
    migrationVersion,
    appData,
    startupPath,
    mode,
    startTime,
  } = data;

  useEffect(() => {
    refetch();
  }, [refetch]);

  return (
    <FieldSet legend={translate('About')}>
      <DescriptionList
        className={classNames(DESCRIPTION_LIST_CLASS, 'mb-[10px]')}
      >
        <DescriptionListItem title={translate('Version')} data={version} />

        {packageVersion && (
          <DescriptionListItem
            title={translate('PackageVersion')}
            data={
              packageAuthor ? (
                <InlineMarkdown
                  data={translate('PackageVersionInfo', {
                    packageVersion,
                    packageAuthor,
                  })}
                />
              ) : (
                packageVersion
              )
            }
          />
        )}

        {isNetCore ? (
          <DescriptionListItem
            title={translate('DotNetVersion')}
            data={`Yes (${runtimeVersion})`}
          />
        ) : null}

        {isContainerized ? (
          <DescriptionListItem title={translate('Docker')} data="Yes" />
        ) : null}

        <DescriptionListItem
          title={translate('Database')}
          data={`${titleCase(databaseType)} ${databaseVersion}`}
        />

        <DescriptionListItem
          title={translate('DatabaseMigration')}
          data={migrationVersion}
        />

        <DescriptionListItem
          title={translate('AppDataDirectory')}
          data={appData}
        />

        <DescriptionListItem
          title={translate('StartupDirectory')}
          data={startupPath}
        />

        <DescriptionListItem title={translate('Mode')} data={titleCase(mode)} />

        <DescriptionListItem
          title={translate('Uptime')}
          data={<StartTime startTime={startTime} />}
        />
      </DescriptionList>
    </FieldSet>
  );
}

export default About;
