import React from 'react';
import Modal from 'Components/Modal/Modal';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import { sizes } from 'Helpers/Props';
import { MovieGrabRelease } from 'InteractiveSearch/useReleases';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import MovieOverrideMatchModalContent from './MovieOverrideMatchModalContent';

interface MovieOverrideMatchModalProps {
  isOpen: boolean;
  title: string;
  indexerId: number;
  guid: string;
  movieId?: number;
  languages: Language[];
  quality: QualityModel;
  protocol: DownloadProtocol;
  isGrabbing: boolean;
  grabError?: string;
  grabRelease: (payload: MovieGrabRelease) => void;
  onModalClose(): void;
}

function MovieOverrideMatchModal(props: MovieOverrideMatchModalProps) {
  const {
    isOpen,
    title,
    indexerId,
    guid,
    movieId,
    languages,
    quality,
    protocol,
    isGrabbing,
    grabError,
    grabRelease,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} size={sizes.LARGE} onModalClose={onModalClose}>
      <MovieOverrideMatchModalContent
        title={title}
        indexerId={indexerId}
        guid={guid}
        movieId={movieId}
        languages={languages}
        quality={quality}
        protocol={protocol}
        isGrabbing={isGrabbing}
        grabError={grabError}
        grabRelease={grabRelease}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default MovieOverrideMatchModal;
