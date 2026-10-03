import React, { useCallback, useState } from 'react';
import Alert from 'Components/Alert';
import SpinnerButton from 'Components/Link/SpinnerButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import { TranscodeDevice } from '../useMediaCompressionCapabilities';
import {
  TranscodeProfile,
  useCreateTranscodeProfile,
  useDeleteTranscodeProfile,
  useTranscodeProfiles,
  useUpdateTranscodeProfile,
} from '../useTranscodeProfiles';
import TranscodeProfileModal from './TranscodeProfileModal';

const ROW_CLASS =
  'mb-[10px] flex flex-wrap items-center justify-between gap-[10px] rounded-[4px] border border-[#e5e5e5] p-[10px]';
const META_CLASS = 'text-[var(--helpTextColor)]';

function describeProfile(profile: TranscodeProfile) {
  const extras = `${profile.maxHeight ? ` · ${profile.maxHeight}p` : ''}${
    profile.tag ? ` · [${profile.tag}]` : ''
  }${profile.preferEnglishAudio ? ` · ${translate('EnglishAudioFirst')}` : ''}`;

  if (profile.mode === 'Remux') {
    return `Remux → ${(profile.container ?? 'mkv').toUpperCase()}${extras}`;
  }

  if (profile.mode === 'TargetSize') {
    return `${profile.codec?.toUpperCase()} · ${
      profile.targetSizeMB ?? '?'
    } MB${extras}`;
  }

  if (profile.mode === 'PercentageReduction') {
    return `${profile.codec?.toUpperCase()} · -${
      profile.targetPercent ?? '?'
    }%${extras}`;
  }

  return `${profile.codec?.toUpperCase()} · CRF ${
    profile.qualityValue
  }${extras}`;
}

interface ProfileRowProps {
  profile: TranscodeProfile;
  onEdit(profile: TranscodeProfile): void;
  onDelete(profile: TranscodeProfile): void;
}

function ProfileRow({ profile, onEdit, onDelete }: ProfileRowProps) {
  const handleEditPress = useCallback(() => onEdit(profile), [onEdit, profile]);
  const handleDeletePress = useCallback(
    () => onDelete(profile),
    [onDelete, profile]
  );

  return (
    <div className={ROW_CLASS}>
      <div>
        <div className="font-semibold">
          {profile.name}
          {profile.isDefault ? (
            <span className={`ml-[8px] ${META_CLASS}`}>
              ({translate('Default')})
            </span>
          ) : null}
        </div>

        <div className={`mt-[5px] ${META_CLASS}`}>
          {describeProfile(profile)}
          {profile.deviceId ? ` · ${profile.deviceId}` : ''}
        </div>
      </div>

      <div className="flex gap-[10px]">
        <SpinnerButton isSpinning={false} onPress={handleEditPress}>
          {translate('Edit')}
        </SpinnerButton>

        <SpinnerButton
          kind={kinds.DANGER}
          isSpinning={false}
          onPress={handleDeletePress}
        >
          {translate('Delete')}
        </SpinnerButton>
      </div>
    </div>
  );
}

interface ProfilesTableProps {
  devices: TranscodeDevice[];
}

function ProfilesTable({ devices }: ProfilesTableProps) {
  const { isFetching, isFetched, error, profiles } = useTranscodeProfiles();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editing, setEditing] = useState<TranscodeProfile | undefined>();
  const [deleting, setDeleting] = useState<TranscodeProfile | undefined>();
  const [saveError, setSaveError] = useState<string | null>(null);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const { createProfile, isCreating } = useCreateTranscodeProfile();
  const { updateProfile, isUpdating } = useUpdateTranscodeProfile();
  const { deleteProfile, isDeleting } = useDeleteTranscodeProfile();

  const handleAddPress = useCallback(() => {
    setEditing(undefined);
    setSaveError(null);
    setIsModalOpen(true);
  }, []);

  const handleEditPress = useCallback((profile: TranscodeProfile) => {
    setEditing(profile);
    setSaveError(null);
    setIsModalOpen(true);
  }, []);

  const handleDeletePress = useCallback((profile: TranscodeProfile) => {
    setDeleteError(null);
    setDeleting(profile);
  }, []);

  const handleModalClose = useCallback(() => {
    setIsModalOpen(false);
    setEditing(undefined);
    setSaveError(null);
  }, []);

  const handleSave = useCallback(
    (profile: TranscodeProfile) => {
      setSaveError(null);

      const onSuccess = () => handleModalClose();

      const onError = () => {
        setSaveError(translate('TranscodeProfileSaveFailed'));
      };

      if (profile.id === 0) {
        createProfile(profile, { onSuccess, onError });
      } else {
        updateProfile(profile, { onSuccess, onError });
      }
    },
    [createProfile, updateProfile, handleModalClose]
  );

  const handleConfirmDelete = useCallback(() => {
    if (!deleting) {
      return;
    }

    setDeleteError(null);

    deleteProfile(deleting.id, {
      onSuccess: () => setDeleting(undefined),
      onError: () => {
        setDeleteError(translate('TranscodeProfileDeleteFailed'));
      },
    });
  }, [deleting, deleteProfile]);

  const handleCancelDelete = useCallback(() => {
    setDeleting(undefined);
    setDeleteError(null);
  }, []);

  return (
    <div>
      <div className="mb-[10px]">
        <SpinnerButton
          kind={kinds.PRIMARY}
          isSpinning={false}
          onPress={handleAddPress}
        >
          {translate('AddTranscodeProfile')}
        </SpinnerButton>
      </div>

      {error ? (
        <Alert kind={kinds.DANGER}>
          {translate('TranscodeProfilesLoadError')}
        </Alert>
      ) : null}

      {!error && isFetched && profiles.length === 0 ? (
        <div className={META_CLASS}>{translate('NoTranscodeProfiles')}</div>
      ) : null}

      {!isFetching || profiles.length > 0
        ? profiles.map((profile) => (
            <ProfileRow
              key={profile.id}
              profile={profile}
              onEdit={handleEditPress}
              onDelete={handleDeletePress}
            />
          ))
        : null}

      <TranscodeProfileModal
        isOpen={isModalOpen}
        profile={editing}
        devices={devices}
        isSaving={isCreating || isUpdating}
        error={saveError}
        onSave={handleSave}
        onModalClose={handleModalClose}
      />

      <ConfirmModal
        isOpen={!!deleting}
        kind={kinds.DANGER}
        title={translate('DeleteTranscodeProfile')}
        message={
          <>
            {translate('DeleteTranscodeProfileMessage', {
              name: deleting?.name ?? '',
            })}

            {deleteError ? (
              <Alert kind={kinds.DANGER}>{deleteError}</Alert>
            ) : null}
          </>
        }
        confirmLabel={translate('Delete')}
        isSpinning={isDeleting}
        onConfirm={handleConfirmDelete}
        onCancel={handleCancelDelete}
      />
    </div>
  );
}

export default ProfilesTable;
