import React, { useCallback, useState } from 'react';
import FieldSet from 'Components/FieldSet';
import IconButton from 'Components/Link/IconButton';
import PageSectionContent from 'Components/Page/PageSectionContent';
import Scroller from 'Components/Scroller/Scroller';
import { icons, scrollDirections } from 'Helpers/Props';
import { useTagList } from 'Tags/useTags';
import translate from 'Utilities/String/translate';
import DelayProfile from './DelayProfile';
import EditDelayProfileModal from './EditDelayProfileModal';
import {
  useReorderDelayProfile,
  useSortedDelayProfiles,
} from './useDelayProfiles';

function DelayProfiles() {
  const {
    error,
    isFetching,
    isFetched: isPopulated,
    items,
    defaultProfile,
  } = useSortedDelayProfiles();

  const { reorderDelayProfile } = useReorderDelayProfile();

  const tagList = useTagList();

  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dropIndex, setDropIndex] = useState<number | null>(null);
  const [isAddDelayProfileModalOpen, setIsAddDelayProfileModalOpen] =
    useState(false);

  const isDragging = dropIndex !== null;
  const isDraggingUp =
    isDragging &&
    dropIndex != null &&
    dragIndex != null &&
    dropIndex < dragIndex;
  const isDraggingDown =
    isDragging &&
    dropIndex != null &&
    dragIndex != null &&
    dropIndex > dragIndex;

  const handleAddDelayProfilePress = useCallback(() => {
    setIsAddDelayProfileModalOpen(true);
  }, []);

  const handleAddDelayProfileModalClose = useCallback(() => {
    setIsAddDelayProfileModalOpen(false);
  }, []);

  const handleDelayProfileDragMove = useCallback(
    (newDragIndex: number, newDropIndex: number) => {
      setDragIndex(newDragIndex);
      setDropIndex(newDropIndex);
    },
    []
  );

  const handleDelayProfileDragEnd = useCallback(
    (id: number, didDrop: boolean) => {
      if (didDrop && dropIndex !== null) {
        const moveOrder = dropIndex;
        const moving = items.find((p) => p.id === id);

        if (moving && moving.order !== moveOrder) {
          const after =
            moveOrder > 1 ? items.find((p) => p.order === moveOrder - 1) : null;

          reorderDelayProfile({ id, after: after?.id });
        }
      }

      setDragIndex(null);
      setDropIndex(null);
    },
    [dropIndex, items, reorderDelayProfile]
  );

  return (
    <FieldSet legend={translate('DelayProfiles')}>
      <PageSectionContent
        errorMessage={translate('DelayProfilesLoadError')}
        error={error}
        isFetching={isFetching}
        isPopulated={isPopulated}
      >
        <Scroller
          className="w-full overflow-x-auto overflow-y-hidden md:w-auto md:overflow-y-visible"
          scrollDirection={scrollDirections.HORIZONTAL}
          autoFocus={false}
        >
          <div>
            <div className="mb-[10px] flex font-bold">
              <div className="shrink-0 grow-0 basis-[200px]">
                {translate('PreferredProtocol')}
              </div>
              <div className="shrink-0 grow-0 basis-[200px]">
                {translate('UsenetDelay')}
              </div>
              <div className="shrink-0 grow-0 basis-[200px]">
                {translate('TorrentDelay')}
              </div>
              <div className="grow shrink-0 basis-auto">
                {translate('Tags')}
              </div>
            </div>

            <div className="select-none">
              {items.map((item) => {
                return (
                  <DelayProfile
                    key={item.id}
                    {...item}
                    tagList={tagList}
                    isDraggingUp={isDraggingUp}
                    isDraggingDown={isDraggingDown}
                    onDelayProfileDragEnd={handleDelayProfileDragEnd}
                    onDelayProfileDragMove={handleDelayProfileDragMove}
                  />
                );
              })}
            </div>

            {defaultProfile ? (
              <div>
                <DelayProfile
                  {...defaultProfile}
                  tagList={tagList}
                  isDraggingDown={false}
                  isDraggingUp={false}
                  onDelayProfileDragEnd={handleDelayProfileDragEnd}
                  onDelayProfileDragMove={handleDelayProfileDragMove}
                />
              </div>
            ) : null}
          </div>
        </Scroller>

        <div className="flex justify-end">
          <IconButton
            className="w-[40px] text-center"
            name={icons.ADD}
            aria-label={translate('AddDelayProfile')}
            title={translate('AddDelayProfile')}
            onPress={handleAddDelayProfilePress}
          />
        </div>

        <EditDelayProfileModal
          isOpen={isAddDelayProfileModalOpen}
          onModalClose={handleAddDelayProfileModalClose}
        />
      </PageSectionContent>
    </FieldSet>
  );
}

export default DelayProfiles;
