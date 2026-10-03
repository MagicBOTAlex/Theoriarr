import React, { useCallback, useState } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import IconButton from 'Components/Link/IconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditQualityProfileModal from './EditQualityProfileModal';
import {
  QualityProfileItems,
  useDeleteQualityProfile,
} from './useQualityProfiles';

const NAME_CONTAINER_CLASS = 'flex justify-between';
const NAME_CLASS =
  'overflow-hidden! mb-[20px] max-w-full text-ellipsis! whitespace-nowrap! font-light text-[24px]';

interface QualityProfileProps {
  id: number;
  name: string;
  upgradeAllowed: boolean;
  cutoff: number;
  items: QualityProfileItems;
  isDeleting: boolean;
  onCloneQualityProfilePress: (id: number) => void;
}

function QualityProfile({
  id,
  name,
  upgradeAllowed,
  cutoff,
  items,
  isDeleting,
  onCloneQualityProfilePress,
}: QualityProfileProps) {
  const { deleteQualityProfile } = useDeleteQualityProfile(id);

  const [isEditQualityProfileModalOpen, setIsEditQualityProfileModalOpen] =
    useState(false);

  const [isDeleteQualityProfileModalOpen, setIsDeleteQualityProfileModalOpen] =
    useState(false);

  const handleEditQualityProfilePress = useCallback(() => {
    setIsEditQualityProfileModalOpen(true);
  }, []);

  const handleEditQualityProfileModalClose = useCallback(() => {
    setIsEditQualityProfileModalOpen(false);
  }, []);

  const handleDeleteQualityProfilePress = useCallback(() => {
    setIsDeleteQualityProfileModalOpen(true);
  }, []);

  const handleDeleteQualityProfileModalClose = useCallback(() => {
    setIsDeleteQualityProfileModalOpen(false);
  }, []);

  const handleConfirmDeleteQualityProfile = useCallback(() => {
    deleteQualityProfile();
  }, [deleteQualityProfile]);

  const handleCloneQualityProfilePress = useCallback(() => {
    onCloneQualityProfilePress(id);
  }, [id, onCloneQualityProfilePress]);

  return (
    <Card
      className="w-[300px]"
      overlayContent={true}
      aria-label={translate('EditQualityProfileName', { name })}
      onPress={handleEditQualityProfilePress}
    >
      <div className={NAME_CONTAINER_CLASS}>
        <div className={NAME_CLASS}>{name}</div>

        <IconButton
          className="inline-block mx-0.5 w-[22px] rounded-[4px] text-center [font-size:inherit] hover:bg-[inherit] hover:text-[var(--iconButtonHoverColor)] h-[36px]"
          title={translate('CloneProfile')}
          aria-label={translate('CloneProfile')}
          name={icons.CLONE}
          onPress={handleCloneQualityProfilePress}
        />
      </div>

      <div className="flex flex-wrap mt-[5px] pointer-events-auto">
        {items.map((item) => {
          if (!item.allowed) {
            return null;
          }

          if ('quality' in item) {
            const isCutoff = upgradeAllowed && item.quality.id === cutoff;

            return (
              <Label
                key={item.quality.id}
                kind={isCutoff ? kinds.INFO : kinds.DEFAULT}
                title={
                  isCutoff
                    ? translate('UpgradeUntilThisQualityIsMetOrExceeded')
                    : undefined
                }
              >
                {item.quality.name}
              </Label>
            );
          }

          const isCutoff = upgradeAllowed && item.id === cutoff;

          return (
            <Tooltip
              key={item.id}
              className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default m-0! border-none!"
              anchor={
                <Label
                  kind={isCutoff ? kinds.INFO : kinds.DEFAULT}
                  title={isCutoff ? translate('Cutoff') : undefined}
                >
                  {item.name}
                </Label>
              }
              tooltip={
                <div>
                  {item.items.map((groupItem) => {
                    return (
                      <Label
                        key={groupItem.quality.id}
                        kind={isCutoff ? kinds.INFO : kinds.DEFAULT}
                        title={isCutoff ? translate('Cutoff') : undefined}
                      >
                        {groupItem.quality.name}
                      </Label>
                    );
                  })}
                </div>
              }
              kind={kinds.INVERSE}
              position={tooltipPositions.TOP}
            />
          );
        })}
      </div>

      <EditQualityProfileModal
        id={id}
        isOpen={isEditQualityProfileModalOpen}
        onModalClose={handleEditQualityProfileModalClose}
        onDeleteQualityProfilePress={handleDeleteQualityProfilePress}
      />

      <ConfirmModal
        isOpen={isDeleteQualityProfileModalOpen}
        kind={kinds.DANGER}
        title={translate('DeleteQualityProfile')}
        message={translate('DeleteQualityProfileMessageText', { name })}
        confirmLabel={translate('Delete')}
        isSpinning={isDeleting}
        onConfirm={handleConfirmDeleteQualityProfile}
        onCancel={handleDeleteQualityProfileModalClose}
      />
    </Card>
  );
}

export default QualityProfile;
