import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import { SelectedSchema } from 'Settings/useProviderSchema';
import translate from 'Utilities/String/translate';
import AddDownloadClientModal from './AddDownloadClientModal';
import DownloadClient from './DownloadClient';
import EditDownloadClientModal from './EditDownloadClientModal';
import { useSortedDownloadClients } from './useDownloadClients';

const DOWNLOAD_CLIENTS_CLASS = 'flex flex-wrap';
const CENTER_CLASS =
  'inline-block p-[5px_20px_0] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardCenterBackgroundColor)]';

function DownloadClients() {
  const { isFetching, isFetched, data, error } = useSortedDownloadClients();

  const [isAddDownloadClientModalOpen, setIsAddDownloadClientModalOpen] =
    useState(false);
  const [isEditDownloadClientModalOpen, setIsEditDownloadClientModalOpen] =
    useState(false);
  const [cloneDownloadClientId, setCloneDownloadClientId] = useState<
    number | null
  >(null);
  const [selectedSchema, setSelectedSchema] = useState<
    SelectedSchema | undefined
  >(undefined);

  const handleAddDownloadClientPress = useCallback(() => {
    setCloneDownloadClientId(null);
    setIsAddDownloadClientModalOpen(true);
  }, []);

  const handleCloneDownloadClientPress = useCallback((id: number) => {
    setCloneDownloadClientId(id);
    setIsEditDownloadClientModalOpen(true);
  }, []);

  const handleDownloadClientSelect = useCallback((selected: SelectedSchema) => {
    setSelectedSchema(selected);
    setIsAddDownloadClientModalOpen(false);
    setIsEditDownloadClientModalOpen(true);
  }, []);

  const handleAddDownloadClientModalClose = useCallback(() => {
    setIsAddDownloadClientModalOpen(false);
  }, []);

  const handleEditDownloadClientModalClose = useCallback(() => {
    setCloneDownloadClientId(null);
    setIsEditDownloadClientModalOpen(false);
  }, []);

  return (
    <FieldSet legend={translate('DownloadClients')}>
      <PageSectionContent
        errorMessage={translate('DownloadClientsLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isFetched}
      >
        <div className={DOWNLOAD_CLIENTS_CLASS}>
          {data.map((item) => {
            return (
              <DownloadClient
                key={item.id}
                {...item}
                onCloneDownloadClientPress={handleCloneDownloadClientPress}
              />
            );
          })}

          <Card
            className="w-[290px] bg-[var(--cardAlternateBackgroundColor)]! text-[var(--gray)] text-center"
            aria-label={translate('AddDownloadClient')}
            onPress={handleAddDownloadClientPress}
          >
            <div className={CENTER_CLASS}>
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </div>

        <AddDownloadClientModal
          isOpen={isAddDownloadClientModalOpen}
          onDownloadClientSelect={handleDownloadClientSelect}
          onModalClose={handleAddDownloadClientModalClose}
        />

        <EditDownloadClientModal
          isOpen={isEditDownloadClientModalOpen}
          cloneId={cloneDownloadClientId ?? undefined}
          selectedSchema={selectedSchema}
          onModalClose={handleEditDownloadClientModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default DownloadClients;
