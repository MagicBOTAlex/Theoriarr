import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import { SelectedSchema } from 'Settings/useProviderSchema';
import translate from 'Utilities/String/translate';
import AddImportListModal from './AddImportListModal';
import EditImportListModal from './EditImportListModal';
import ImportList from './ImportList';
import { useSortedImportLists } from './useImportLists';

function ImportLists() {
  const { isFetching, isFetched, data, error } = useSortedImportLists();

  const [isAddImportListModalOpen, setIsAddImportListModalOpen] =
    useState(false);
  const [isEditImportListModalOpen, setIsEditImportListModalOpen] =
    useState(false);
  const [cloneImportListId, setCloneImportListId] = useState<number | null>(
    null
  );

  const [selectedSchema, setSelectedSchema] = useState<
    SelectedSchema | undefined
  >(undefined);

  const handleAddImportListPress = useCallback(() => {
    setCloneImportListId(null);
    setIsAddImportListModalOpen(true);
  }, []);

  const handleAddImportListModalClose = useCallback(() => {
    setIsAddImportListModalOpen(false);
  }, []);

  const handleImportListSelect = useCallback((selected: SelectedSchema) => {
    setSelectedSchema(selected);
    setIsAddImportListModalOpen(false);
    setIsEditImportListModalOpen(true);
  }, []);

  const handleEditImportListModalClose = useCallback(() => {
    setCloneImportListId(null);
    setIsEditImportListModalOpen(false);
  }, []);

  const handleCloneImportListPress = useCallback((id: number) => {
    setCloneImportListId(id);
    setIsEditImportListModalOpen(true);
  }, []);

  return (
    <FieldSet legend={translate('ImportLists')}>
      <PageSectionContent
        errorMessage={translate('ImportListsLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isFetched}
      >
        <div className="flex flex-wrap">
          {data.map((item) => {
            return (
              <ImportList
                key={item.id}
                {...item}
                onCloneImportListPress={handleCloneImportListPress}
              />
            );
          })}

          <Card
            className="w-[290px] bg-[var(--cardAlternateBackgroundColor)]! text-[var(--gray)] text-center"
            aria-label={translate('AddImportList')}
            onPress={handleAddImportListPress}
          >
            <div className="inline-block px-[20px] pt-[5px] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardCenterBackgroundColor)]">
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </div>

        <AddImportListModal
          isOpen={isAddImportListModalOpen}
          onImportListSelect={handleImportListSelect}
          onModalClose={handleAddImportListModalClose}
        />

        <EditImportListModal
          isOpen={isEditImportListModalOpen}
          cloneId={cloneImportListId ?? undefined}
          selectedSchema={selectedSchema}
          onModalClose={handleEditImportListModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default ImportLists;
