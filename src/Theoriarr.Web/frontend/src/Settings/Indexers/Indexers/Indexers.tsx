import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import { SelectedSchema } from 'Settings/useProviderSchema';
import translate from 'Utilities/String/translate';
import { useSortedIndexers } from '../useIndexers';
import AddIndexerModal from './AddIndexerModal';
import EditIndexerModal from './EditIndexerModal';
import Indexer from './Indexer';

const INDEXERS_CLASS = 'flex flex-wrap';
const CENTER_CLASS =
  'inline-block p-[5px_20px_0] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardCenterBackgroundColor)]';

function Indexers() {
  const { isFetching, isFetched, data, error } = useSortedIndexers();

  const [isAddIndexerModalOpen, setIsAddIndexerModalOpen] = useState(false);
  const [isEditIndexerModalOpen, setIsEditIndexerModalOpen] = useState(false);
  const [cloneIndexerId, setCloneIndexerId] = useState<number | null>(null);

  const showPriority = data.some((index) => index.priority !== 25);

  const [selectedSchema, setSelectedSchema] = useState<
    SelectedSchema | undefined
  >(undefined);

  const handleAddIndexerPress = useCallback(() => {
    setCloneIndexerId(null);
    setIsAddIndexerModalOpen(true);
  }, []);

  const handleCloneIndexerPress = useCallback((id: number) => {
    setCloneIndexerId(id);
    setIsEditIndexerModalOpen(true);
  }, []);

  const handleIndexerSelect = useCallback((selected: SelectedSchema) => {
    setSelectedSchema(selected);
    setIsAddIndexerModalOpen(false);
    setIsEditIndexerModalOpen(true);
  }, []);

  const handleAddIndexerModalClose = useCallback(() => {
    setIsAddIndexerModalOpen(false);
  }, []);

  const handleEditIndexerModalClose = useCallback(() => {
    setCloneIndexerId(null);
    setIsEditIndexerModalOpen(false);
  }, []);

  return (
    <FieldSet legend={translate('Indexers')}>
      <PageSectionContent
        errorMessage={translate('IndexersLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isFetched}
      >
        <div className={INDEXERS_CLASS}>
          {data.map((item) => {
            return (
              <Indexer
                key={item.id}
                {...item}
                showPriority={showPriority}
                onCloneIndexerPress={handleCloneIndexerPress}
              />
            );
          })}

          <Card
            className="w-[290px] bg-[var(--cardAlternateBackgroundColor)]! text-[var(--gray)] text-center"
            aria-label={translate('AddIndexer')}
            onPress={handleAddIndexerPress}
          >
            <div className={CENTER_CLASS}>
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </div>

        <AddIndexerModal
          isOpen={isAddIndexerModalOpen}
          onIndexerSelect={handleIndexerSelect}
          onModalClose={handleAddIndexerModalClose}
        />

        <EditIndexerModal
          isOpen={isEditIndexerModalOpen}
          cloneId={cloneIndexerId ?? undefined}
          selectedSchema={selectedSchema}
          onModalClose={handleEditIndexerModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default Indexers;
