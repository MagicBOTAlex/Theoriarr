import React from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import useModalOpenState from 'Helpers/Hooks/useModalOpenState';
import { icons } from 'Helpers/Props';
import { useIndexersData } from 'Settings/Indexers/useIndexers';
import { useTagList } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import EditReleaseProfileModal from './EditReleaseProfileModal';
import ReleaseProfileItem from './ReleaseProfileItem';
import { useReleaseProfiles } from './useReleaseProfiles';

function ReleaseProfiles() {
  const { data, isFetching, isFetched, error } = useReleaseProfiles();

  const tagList = useTagList();
  const indexerList = useIndexersData();

  const [
    isAddReleaseProfileModalOpen,
    setAddReleaseProfileModalOpen,
    setAddReleaseProfileModalClosed,
  ] = useModalOpenState(false);

  return (
    <FieldSet legend={translate('ReleaseProfiles')}>
      <PageSectionContent
        errorMessage={translate('ReleaseProfilesLoadError')}
        isFetching={isFetching}
        isPopulated={isFetched}
        error={error}
      >
        <div className="flex flex-wrap">
          <Card
            className="w-[290px] bg-[var(--cardAlternateBackgroundColor)]! text-[var(--gray)] text-center"
            aria-label={translate('AddReleaseProfile')}
            onPress={setAddReleaseProfileModalOpen}
          >
            <div className="inline-block px-[20px] pt-[5px] border border-solid border-[var(--borderColor)] rounded-[4px] bg-[var(--cardCenterBackgroundColor)]">
              <Icon name={icons.ADD} size={45} />
            </div>
          </Card>

          {data.map((item) => {
            return (
              <ReleaseProfileItem
                key={item.id}
                tagList={tagList}
                indexerList={indexerList}
                {...item}
              />
            );
          })}
        </div>

        <EditReleaseProfileModal
          isOpen={isAddReleaseProfileModalOpen}
          onModalClose={setAddReleaseProfileModalClosed}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default ReleaseProfiles;
