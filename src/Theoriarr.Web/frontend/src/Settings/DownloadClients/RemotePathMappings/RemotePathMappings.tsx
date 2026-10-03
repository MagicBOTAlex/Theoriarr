import React, { useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import IconButton from 'Components/Link/IconButton';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditRemotePathMappingModal from './EditRemotePathMappingModal';
import RemotePathMapping from './RemotePathMapping';
import { useRemotePathMappings } from './useRemotePathMappings';

function RemotePathMappings() {
  const { isFetching, isFetched, error, data } = useRemotePathMappings();

  const [isAddRemotePathMappingModalOpen, setIsAddRemotePathMappingModalOpen] =
    useState(false);

  const handleAddRemotePathMappingPress = useCallback(() => {
    setIsAddRemotePathMappingModalOpen(true);
  }, []);

  const handleAddRemotePathMappingModalClose = useCallback(() => {
    setIsAddRemotePathMappingModalOpen(false);
  }, []);

  return (
    <FieldSet legend={translate('RemotePathMappings')}>
      <PageSectionContent
        errorMessage={translate('RemotePathMappingsLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isFetched}
      >
        <Alert kind={kinds.INFO}>
          <InlineMarkdown
            data={translate('RemotePathMappingsInfo', {
              wikiLink:
                'https://wiki.servarr.com/sonarr/settings#remote-path-mappings',
            })}
          />
        </Alert>

        <div className="mb-[10px] flex font-bold">
          <div className="shrink basis-[300px] max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
            {translate('Host')}
          </div>
          <div className="shrink basis-[400px] max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
            {translate('RemotePath')}
          </div>
          <div className="shrink basis-[400px] max-w-full overflow-hidden! text-ellipsis! whitespace-nowrap!">
            {translate('LocalPath')}
          </div>
        </div>

        <div>
          {data.map((item) => {
            return <RemotePathMapping key={item.id} {...item} />;
          })}
        </div>

        <div className="flex justify-end pr-[10px]">
          <IconButton
            className="text-center"
            name={icons.ADD}
            aria-label={translate('AddRemotePathMapping')}
            title={translate('AddRemotePathMapping')}
            onPress={handleAddRemotePathMappingPress}
          />
        </div>

        <EditRemotePathMappingModal
          isOpen={isAddRemotePathMappingModalOpen}
          onModalClose={handleAddRemotePathMappingModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default RemotePathMappings;
