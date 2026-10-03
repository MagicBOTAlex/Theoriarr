import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import CustomFormat from './CustomFormat';
import EditCustomFormatModal from './EditCustomFormatModal';
import { useSortedCustomFormats } from './useCustomFormats';

function CustomFormats() {
  const {
    data: items,
    error,
    isFetching,
    isFetched: isPopulated,
  } = useSortedCustomFormats();

  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [cloneId, setCloneId] = useState<number>();

  const handleAddCustomFormatPress = useCallback(() => {
    setCloneId(undefined);
    setIsEditModalOpen(true);
  }, []);

  const handleCloneCustomFormatPress = useCallback((id: number) => {
    setCloneId(id);
    setIsEditModalOpen(true);
  }, []);

  const handleEditModalClose = useCallback(() => {
    setIsEditModalOpen(false);
    setCloneId(undefined);
  }, []);

  return (
    <FieldSet legend={translate('CustomFormats')}>
      <PageSectionContent
        errorMessage={translate('CustomFormatsLoadError')}
        isFetching={isFetching}
        isPopulated={isPopulated}
        error={error}
      >
        <div className="flex flex-wrap">
          {items.map((item) => (
            <CustomFormat
              key={item.id}
              {...item}
              onCloneCustomFormatPress={handleCloneCustomFormatPress}
            />
          ))}

          <Card
            className="w-[300px] bg-[var(--cardAlternateBackgroundColor)]! text-[var(--gray)] text-center text-[45px]"
            aria-label={translate('AddCustomFormat')}
            onPress={handleAddCustomFormatPress}
          >
            <div className="inline-block px-[20px] pt-[5px] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardCenterBackgroundColor)]">
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>
        </div>

        <EditCustomFormatModal
          cloneId={cloneId}
          isOpen={isEditModalOpen}
          onModalClose={handleEditModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default CustomFormats;
