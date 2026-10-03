import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import { useTagList } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import AutoTagging from './AutoTagging';
import EditAutoTaggingModal from './EditAutoTaggingModal';
import { useSortedAutoTaggings } from './useAutoTaggings';

const AUTO_TAGGINGS_CLASS = 'flex flex-wrap';
const CENTER_CLASS =
  'inline-block p-[5px_20px_0] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardCenterBackgroundColor)]';

export default function AutoTaggings() {
  const {
    data: items,
    error,
    isFetching,
    isFetched: isPopulated,
  } = useSortedAutoTaggings();

  const tagList = useTagList();
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [cloneId, setCloneId] = useState<number>();

  const onClonePress = useCallback((id: number) => {
    setCloneId(id);
    setIsEditModalOpen(true);
  }, []);

  const onEditPress = useCallback(() => {
    setCloneId(undefined);
    setIsEditModalOpen(true);
  }, []);

  const onEditModalClose = useCallback(() => {
    setIsEditModalOpen(false);
    setCloneId(undefined);
  }, []);

  return (
    <FieldSet legend={translate('AutoTagging')}>
      <PageSectionContent
        errorMessage={translate('AutoTaggingLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isPopulated}
      >
        <div className={AUTO_TAGGINGS_CLASS}>
          {items.map((item) => {
            return (
              <AutoTagging
                key={item.id}
                {...item}
                tagList={tagList}
                onCloneAutoTaggingPress={onClonePress}
              />
            );
          })}

          <Card
            className="w-[300px] bg-[var(--cardAlternateBackgroundColor)]! text-[var(--gray)] text-center text-[45px]"
            aria-label={translate('AddAutoTag')}
            onPress={onEditPress}
          >
            <div className={CENTER_CLASS}>
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </div>

        <EditAutoTaggingModal
          isOpen={isEditModalOpen}
          cloneId={cloneId}
          onModalClose={onEditModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}
