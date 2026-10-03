import classNames from 'classnames';
import _ from 'lodash';
import React from 'react';
import DescriptionList, {
  DESCRIPTION_LIST_CLASS,
} from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import { DESCRIPTION_ITEM_DESCRIPTION_CLASS } from 'Components/DescriptionList/DescriptionListItemDescription';
import { DESCRIPTION_ITEM_TITLE_CLASS } from 'Components/DescriptionList/DescriptionListItemTitle';
import Icon from 'Components/Icon';
import Popover from 'Components/Tooltip/Popover';
import { icons, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

const CONTAINER_CLASS =
  'm-[2px] px-[2px] border border-solid rounded-[2px] whitespace-nowrap text-[12px] cursor-default';
const MESSAGES_CLASS = 'mt-[15px]';
const DESCRIPTION_LIST_CLASSES = classNames(
  DESCRIPTION_LIST_CLASS,
  'mr-[10px]'
);
const TITLE_CLASS = classNames(DESCRIPTION_ITEM_TITLE_CLASS, 'w-[80px]!');
const DESCRIPTION_CLASS = classNames(
  DESCRIPTION_ITEM_DESCRIPTION_CLASS,
  'ml-[100px]!'
);

const LEVEL_CLASSES = {
  mixed: 'border-[var(--dangerColor)] text-[var(--dangerColor)]',
  unknown: 'border-[var(--warningColor)] text-[var(--warningColor)]',
  mapped: 'border-[var(--textColor)] text-[var(--textColor)]',
  normal: 'border-[var(--textColor)] text-[var(--textColor)]',
  none: 'border-[var(--textColor)] text-[var(--textColor)] opacity-20 hover:opacity-100',
  notRequested: 'border-[var(--dangerColor)] text-[var(--dangerColor)]',
};

function formatReleaseNumber(
  seasonNumber: number | null | undefined,
  episodeNumbers: number[] | undefined,
  absoluteEpisodeNumbers: number[] | undefined
) {
  if (episodeNumbers && episodeNumbers.length) {
    if (episodeNumbers.length > 1) {
      return `${seasonNumber}x${episodeNumbers[0]}-${
        episodeNumbers[episodeNumbers.length - 1]
      }`;
    }

    return `${seasonNumber}x${episodeNumbers[0]}`;
  }

  if (absoluteEpisodeNumbers && absoluteEpisodeNumbers.length) {
    if (absoluteEpisodeNumbers.length > 1) {
      return `${absoluteEpisodeNumbers[0]}-${
        absoluteEpisodeNumbers[absoluteEpisodeNumbers.length - 1]
      }`;
    }

    return absoluteEpisodeNumbers[0];
  }

  if (seasonNumber != null) {
    return translate('SeasonNumberToken', { seasonNumber });
  }

  return null;
}

interface ReleaseSceneIndicatorProps {
  className: string;
  seasonNumber?: number;
  episodeNumbers?: number[];
  absoluteEpisodeNumbers?: number[];
  sceneSeasonNumber?: number | null;
  sceneEpisodeNumbers?: number[];
  sceneAbsoluteEpisodeNumbers?: number[];
  sceneMapping?: {
    sceneOrigin?: string;
    title?: string;
    comment?: string;
  };
  episodeRequested: boolean;
  isDaily: boolean;
}

function ReleaseSceneIndicator(props: ReleaseSceneIndicatorProps) {
  const {
    className,
    seasonNumber,
    episodeNumbers,
    absoluteEpisodeNumbers,
    sceneSeasonNumber,
    sceneEpisodeNumbers,
    sceneAbsoluteEpisodeNumbers,
    sceneMapping = {},
    episodeRequested,
    isDaily,
  } = props;

  const { sceneOrigin, title, comment } = sceneMapping;

  if (isDaily) {
    return null;
  }

  let mappingDifferent =
    sceneSeasonNumber !== undefined && seasonNumber !== sceneSeasonNumber;

  if (sceneEpisodeNumbers !== undefined) {
    mappingDifferent =
      mappingDifferent || !_.isEqual(sceneEpisodeNumbers, episodeNumbers);
  } else if (sceneAbsoluteEpisodeNumbers !== undefined) {
    mappingDifferent =
      mappingDifferent ||
      !_.isEqual(sceneAbsoluteEpisodeNumbers, absoluteEpisodeNumbers);
  }

  if (!sceneMapping && !mappingDifferent) {
    return null;
  }

  const releaseNumber = formatReleaseNumber(
    sceneSeasonNumber,
    sceneEpisodeNumbers,
    sceneAbsoluteEpisodeNumbers
  );
  const mappedNumber = formatReleaseNumber(
    seasonNumber,
    episodeNumbers,
    absoluteEpisodeNumbers
  );
  const messages = [];

  const isMixed = sceneOrigin === 'mixed';
  const isUnknown = sceneOrigin === 'unknown' || sceneOrigin === 'unknown:tvdb';

  let level = LEVEL_CLASSES.none;

  if (isMixed) {
    level = LEVEL_CLASSES.mixed;
    messages.push(
      <div key="source">
        {translate('ReleaseSceneIndicatorSourceMessage', {
          message: comment ?? 'Source',
        })}
      </div>
    );
  } else if (isUnknown) {
    level = LEVEL_CLASSES.unknown;
    messages.push(
      <div key="unknown">
        {translate('ReleaseSceneIndicatorUnknownMessage')}
      </div>
    );

    if (sceneOrigin === 'unknown') {
      messages.push(
        <div key="origin">
          {translate('ReleaseSceneIndicatorAssumingScene')}.
        </div>
      );
    } else if (sceneOrigin === 'unknown:tvdb') {
      messages.push(
        <div key="origin">{translate('ReleaseSceneIndicatorAssumingTvdb')}</div>
      );
    }
  } else if (mappingDifferent) {
    level = LEVEL_CLASSES.mapped;
  } else if (sceneOrigin) {
    level = LEVEL_CLASSES.normal;
  }

  if (!episodeRequested) {
    if (!isMixed && !isUnknown) {
      level = LEVEL_CLASSES.notRequested;
    }

    if (mappedNumber) {
      messages.push(
        <div key="not-requested">
          {translate('ReleaseSceneIndicatorMappedNotRequested')}
        </div>
      );
    } else {
      messages.push(
        <div key="unknown-series">
          {translate('ReleaseSceneIndicatorUnknownSeries')}
        </div>
      );
    }
  }

  const table = (
    <DescriptionList className={DESCRIPTION_LIST_CLASSES}>
      {comment !== undefined && (
        <DescriptionListItem
          titleClassName={TITLE_CLASS}
          descriptionClassName={DESCRIPTION_CLASS}
          title={translate('Mapping')}
          data={comment}
        />
      )}

      {title !== undefined && (
        <DescriptionListItem
          titleClassName={TITLE_CLASS}
          descriptionClassName={DESCRIPTION_CLASS}
          title={translate('Title')}
          data={title}
        />
      )}

      {releaseNumber !== undefined && (
        <DescriptionListItem
          titleClassName={TITLE_CLASS}
          descriptionClassName={DESCRIPTION_CLASS}
          title={translate('Release')}
          data={releaseNumber ?? 'unknown'}
        />
      )}

      {releaseNumber !== undefined && (
        <DescriptionListItem
          titleClassName={TITLE_CLASS}
          descriptionClassName={DESCRIPTION_CLASS}
          title={translate('TheTvdb')}
          data={mappedNumber ?? 'unknown'}
        />
      )}
    </DescriptionList>
  );

  return (
    <Popover
      anchor={
        <div className={classNames(level, CONTAINER_CLASS, className)}>
          <Icon name={icons.SCENE_MAPPING} />
        </div>
      }
      title={translate('SceneInfo')}
      body={
        <div>
          {table}
          {(messages.length && (
            <div className={MESSAGES_CLASS}>{messages}</div>
          )) ||
            null}
        </div>
      }
      position={tooltipPositions.RIGHT}
    />
  );
}

export default ReleaseSceneIndicator;
