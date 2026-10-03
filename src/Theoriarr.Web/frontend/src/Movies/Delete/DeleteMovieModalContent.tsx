import React, { useCallback, useState } from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { icons, inputTypes, kinds } from 'Helpers/Props';
import { Statistics } from 'Movies/Movie';
import { useDeleteMovie, useSingleMovie } from 'Movies/useMovies';
import { CheckInputChanged } from 'typings/inputs';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';

export interface DeleteMovieModalContentProps {
  movieId: number;
  onModalClose: () => void;
}

function DeleteMovieModalContent({
  movieId,
  onModalClose,
}: DeleteMovieModalContentProps) {
  const {
    title,
    path,
    statistics = {} as Statistics,
  } = useSingleMovie(movieId)!;

  const { movieFileCount = 0, sizeOnDisk = 0 } = statistics;

  const [deleteFiles, setDeleteFiles] = useState(false);
  const [addImportExclusion, setAddImportExclusion] = useState(false);

  const { deleteMovie } = useDeleteMovie(movieId, {
    deleteFiles,
    addImportExclusion,
  });

  const handleDeleteFilesChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setDeleteFiles(value);
    },
    []
  );

  const handleDeleteOptionChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setAddImportExclusion(value);
    },
    []
  );

  const handleDeleteMovieConfirmed = useCallback(() => {
    deleteMovie();

    onModalClose();
  }, [deleteMovie, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('DeleteHeader', { title })}</ModalHeader>

      <ModalBody>
        <div className="mb-5">
          <Icon className="mr-2" name={icons.FOLDER} />

          {path}
        </div>

        <FormGroup>
          <FormLabel>{translate('AddListExclusion')}</FormLabel>

          <FormInputGroup
            type={inputTypes.CHECK}
            name="addImportExclusion"
            value={addImportExclusion}
            helpText={translate('AddListExclusionMovieHelpText')}
            kind={kinds.DANGER}
            onChange={handleDeleteOptionChange}
          />
        </FormGroup>

        <FormGroup>
          <FormLabel>
            {movieFileCount === 0
              ? translate('DeleteMovieFolder')
              : translate('DeleteMovieFiles', { movieFileCount })}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.CHECK}
            name="deleteFiles"
            value={deleteFiles}
            helpText={
              movieFileCount === 0
                ? translate('DeleteMovieFolderHelpText')
                : translate('DeleteMovieFilesHelpText')
            }
            kind={kinds.DANGER}
            onChange={handleDeleteFilesChange}
          />
        </FormGroup>

        {deleteFiles ? (
          <div className="mt-5 text-[var(--dangerColor)]">
            <div>
              <InlineMarkdown
                data={translate('DeleteMovieFolderConfirmation', {
                  path: path ?? '',
                })}
                blockClassName="font-bold font-[var(--defaultFontFamily)]"
              />
            </div>

            {movieFileCount ? (
              <div className="mt-5 text-[var(--warningColor)]">
                {translate('DeleteMovieFolderMovieCount', {
                  movieFileCount,
                  size: formatBytes(sizeOnDisk),
                })}
              </div>
            ) : null}
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>

        <Button kind={kinds.DANGER} onPress={handleDeleteMovieConfirmed}>
          {translate('Delete')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default DeleteMovieModalContent;
