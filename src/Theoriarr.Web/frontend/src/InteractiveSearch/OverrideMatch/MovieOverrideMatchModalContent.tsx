import React, { useCallback, useEffect, useState } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import usePrevious from 'Helpers/Hooks/usePrevious';
import SelectLanguageModal from 'InteractiveImport/Language/SelectLanguageModal';
import SelectMovieModal from 'InteractiveImport/Movie/SelectMovieModal';
import SelectQualityModal from 'InteractiveImport/Quality/SelectQualityModal';
import { MovieGrabRelease } from 'InteractiveSearch/useReleases';
import Language from 'Language/Language';
import { Movie } from 'Movies/Movie';
import useMovies from 'Movies/useMovies';
import { QualityModel } from 'Quality/Quality';
import { useEnabledDownloadClients } from 'Settings/DownloadClients/DownloadClients/useDownloadClients';
import translate from 'Utilities/String/translate';
import SelectDownloadClientModal from './DownloadClient/SelectDownloadClientModal';
import OverrideMatchData from './OverrideMatchData';

const ITEM_CLASS = 'block mb-[5px] ml-[50px] max-[768px]:ml-0';

const FOOTER_EXTRA_CLASS =
  'flex justify-between! overflow-hidden max-[768px]:block';

const ERROR_CLASS =
  'mr-[20px] text-[var(--dangerColor)] [word-break:break-word] max-[768px]:mr-0 max-[768px]:mb-[10px]';

const BUTTONS_CLASS = 'flex max-[768px]:justify-between max-[768px]:grow';

type SelectType =
  | 'select'
  | 'movie'
  | 'quality'
  | 'language'
  | 'downloadClient';

interface MovieOverrideMatchModalContentProps {
  indexerId: number;
  title: string;
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

function MovieOverrideMatchModalContent(
  props: MovieOverrideMatchModalContentProps
) {
  const modalTitle = translate('ManualGrab');
  const {
    indexerId,
    title,
    guid,
    protocol,
    isGrabbing,
    grabError,
    grabRelease,
    onModalClose,
  } = props;

  const [movieId, setMovieId] = useState(props.movieId);
  const [languages, setLanguages] = useState(props.languages);
  const [quality, setQuality] = useState(props.quality);
  const [downloadClientId, setDownloadClientId] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectModalOpen, setSelectModalOpen] = useState<SelectType | null>(
    null
  );
  const previousIsGrabbing = usePrevious(isGrabbing);

  const { data: allMovies } = useMovies();
  const movie: Movie | undefined = allMovies.find((m) => m.id === movieId);

  const { data: downloadClients } = useEnabledDownloadClients(protocol);

  const onSelectModalClose = useCallback(() => {
    setSelectModalOpen(null);
  }, [setSelectModalOpen]);

  const onSelectMoviePress = useCallback(() => {
    setSelectModalOpen('movie');
  }, [setSelectModalOpen]);

  const onMovieSelect = useCallback(
    (m: Movie) => {
      setMovieId(m.id);
      setSelectModalOpen(null);
    },
    [setMovieId, setSelectModalOpen]
  );

  const onSelectQualityPress = useCallback(() => {
    setSelectModalOpen('quality');
  }, [setSelectModalOpen]);

  const onQualitySelect = useCallback(
    (selectedQuality: QualityModel) => {
      setQuality(selectedQuality);
      setSelectModalOpen(null);
    },
    [setQuality, setSelectModalOpen]
  );

  const onSelectLanguagesPress = useCallback(() => {
    setSelectModalOpen('language');
  }, [setSelectModalOpen]);

  const onLanguagesSelect = useCallback(
    (selectedLanguages: Language[]) => {
      setLanguages(selectedLanguages);
      setSelectModalOpen(null);
    },
    [setLanguages, setSelectModalOpen]
  );

  const onSelectDownloadClientPress = useCallback(() => {
    setSelectModalOpen('downloadClient');
  }, [setSelectModalOpen]);

  const onDownloadClientSelect = useCallback(
    (selectedDownloadClientId: number) => {
      setDownloadClientId(selectedDownloadClientId);
      setSelectModalOpen(null);
    },
    [setDownloadClientId, setSelectModalOpen]
  );

  const onGrabPress = useCallback(() => {
    if (!movieId) {
      setError(translate('OverrideGrabNoMovie'));
      return;
    } else if (!quality) {
      setError(translate('OverrideGrabNoQuality'));
      return;
    } else if (!languages.length) {
      setError(translate('OverrideGrabNoLanguage'));
      return;
    }

    grabRelease({
      indexerId,
      guid,
      movieId,
      quality,
      languages,
      downloadClientId,
      shouldOverride: true,
    });
  }, [
    indexerId,
    guid,
    movieId,
    quality,
    languages,
    downloadClientId,
    grabRelease,
    setError,
  ]);

  useEffect(() => {
    if (!isGrabbing && previousIsGrabbing) {
      onModalClose();
    }
  }, [isGrabbing, previousIsGrabbing, onModalClose]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {translate('OverrideGrabModalTitle', { title })}
      </ModalHeader>

      <ModalBody>
        <DescriptionList>
          <DescriptionListItem
            className={ITEM_CLASS}
            title={translate('Movie')}
            data={
              <OverrideMatchData
                value={movie?.title}
                onPress={onSelectMoviePress}
              />
            }
          />

          <DescriptionListItem
            className={ITEM_CLASS}
            title={translate('Quality')}
            data={
              <OverrideMatchData
                value={
                  <EpisodeQuality
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default cursor-pointer"
                    quality={quality}
                    showRevision={false}
                  />
                }
                onPress={onSelectQualityPress}
              />
            }
          />

          <DescriptionListItem
            className={ITEM_CLASS}
            title={translate('Languages')}
            data={
              <OverrideMatchData
                value={
                  <EpisodeLanguages
                    className="inline-block m-[2px] rounded-[2px] border border-solid text-center whitespace-nowrap leading-none cursor-default cursor-pointer"
                    languages={languages}
                  />
                }
                onPress={onSelectLanguagesPress}
              />
            }
          />

          {downloadClients.length > 1 ? (
            <DescriptionListItem
              className={ITEM_CLASS}
              title={translate('DownloadClient')}
              data={
                <OverrideMatchData
                  value={
                    downloadClients.find(
                      (downloadClient) => downloadClient.id === downloadClientId
                    )?.name ?? translate('Default')
                  }
                  onPress={onSelectDownloadClientPress}
                />
              }
            />
          ) : null}
        </DescriptionList>
      </ModalBody>

      <ModalFooter className={FOOTER_EXTRA_CLASS}>
        <div className={ERROR_CLASS}>{error || grabError}</div>

        <div className={BUTTONS_CLASS}>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerErrorButton
            isSpinning={isGrabbing}
            error={grabError}
            onPress={onGrabPress}
          >
            {translate('GrabRelease')}
          </SpinnerErrorButton>
        </div>
      </ModalFooter>

      <SelectMovieModal
        isOpen={selectModalOpen === 'movie'}
        modalTitle={modalTitle}
        onMovieSelect={onMovieSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectQualityModal
        isOpen={selectModalOpen === 'quality'}
        qualityId={quality ? quality.quality.id : 0}
        proper={quality ? quality.revision.version > 1 : false}
        real={quality ? quality.revision.real > 0 : false}
        modalTitle={modalTitle}
        onQualitySelect={onQualitySelect}
        onModalClose={onSelectModalClose}
      />

      <SelectLanguageModal
        isOpen={selectModalOpen === 'language'}
        languageIds={languages ? languages.map((l) => l.id) : []}
        modalTitle={modalTitle}
        onLanguagesSelect={onLanguagesSelect}
        onModalClose={onSelectModalClose}
      />

      <SelectDownloadClientModal
        isOpen={selectModalOpen === 'downloadClient'}
        protocol={protocol}
        modalTitle={modalTitle}
        onDownloadClientSelect={onDownloadClientSelect}
        onModalClose={onSelectModalClose}
      />
    </ModalContent>
  );
}

export default MovieOverrideMatchModalContent;
