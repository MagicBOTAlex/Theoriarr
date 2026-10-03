import classNames from 'classnames';
import React from 'react';
import { ColorImpairedConsumer } from 'App/ColorImpairedContext';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItem from 'Components/DescriptionList/DescriptionListItem';
import formatBytes from 'Utilities/Number/formatBytes';
import translate from 'Utilities/String/translate';
import useLibraryItems from './useLibraryItems';

const LEGEND_ITEM_COLOR_CLASS = 'mr-2 w-[30px] h-4 rounded-[4px]';
const FOOTER_CLASS = 'flex flex-wrap mt-[20px] text-[12px] max-[768px]:block';
const LEGEND_ITEM_CLASS = 'flex mb-[4px] leading-[16px]';
const STATISTICS_CLASS =
  'flex justify-between flex-wrap max-[1200px]:block max-[768px]:flex max-[768px]:mt-[20px]';
const MISSING_MONITORED_CLASS =
  'bg-[var(--dangerColor)] [&.colorImpaired]:[background:repeating-linear-gradient(90deg,rgb(228,76,76),rgb(228,76,76)_5px,rgb(204,68,68)_5px,rgb(204,68,68)_10px)]';
const MISSING_UNMONITORED_CLASS =
  'bg-[var(--warningColor)] [&.colorImpaired]:[background:repeating-linear-gradient(45deg,#ffa500,#ffa500_5px,rgb(255,179,38)_5px,rgb(255,179,38)_10px)]';

export default function LibraryIndexFooter() {
  const { items } = useLibraryItems();

  let seriesCount = 0;
  let movieCount = 0;
  let episodes = 0;
  let episodeFiles = 0;
  let ended = 0;
  let continuing = 0;
  let monitored = 0;
  let moviesWithFile = 0;
  let totalFileSize = 0;

  items.forEach((item) => {
    totalFileSize += item.sizeOnDisk ?? 0;

    if (item.monitored) {
      monitored++;
    }

    if (item.type === 'series') {
      seriesCount++;

      const statistics = item.series?.statistics;

      episodes += statistics?.episodeCount ?? 0;
      episodeFiles += statistics?.episodeFileCount ?? 0;

      if (item.status === 'ended') {
        ended++;
      } else {
        continuing++;
      }
    } else {
      movieCount++;

      if (item.hasFile) {
        moviesWithFile++;
      }
    }
  });

  return (
    <ColorImpairedConsumer>
      {(enableColorImpairedMode) => {
        return (
          <div className={FOOTER_CLASS}>
            <div>
              <div className={LEGEND_ITEM_CLASS}>
                <div
                  className={classNames(
                    LEGEND_ITEM_COLOR_CLASS,
                    'bg-[var(--primaryColor)]',
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('SeriesIndexFooterContinuing')}</div>
              </div>

              <div className={LEGEND_ITEM_CLASS}>
                <div
                  className={classNames(
                    LEGEND_ITEM_COLOR_CLASS,
                    'bg-[var(--successColor)]',
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('SeriesIndexFooterEnded')}</div>
              </div>

              <div className={LEGEND_ITEM_CLASS}>
                <div
                  className={classNames(
                    LEGEND_ITEM_COLOR_CLASS,
                    MISSING_MONITORED_CLASS,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('SeriesIndexFooterMissingMonitored')}</div>
              </div>

              <div className={LEGEND_ITEM_CLASS}>
                <div
                  className={classNames(
                    LEGEND_ITEM_COLOR_CLASS,
                    MISSING_UNMONITORED_CLASS,
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('SeriesIndexFooterMissingUnmonitored')}</div>
              </div>

              <div className={LEGEND_ITEM_CLASS}>
                <div
                  className={classNames(
                    LEGEND_ITEM_COLOR_CLASS,
                    'bg-[var(--purple)]',
                    enableColorImpairedMode && 'colorImpaired'
                  )}
                />
                <div>{translate('SeriesIndexFooterDownloading')}</div>
              </div>
            </div>

            <div className={STATISTICS_CLASS}>
              <DescriptionList>
                <DescriptionListItem
                  title={translate('Series')}
                  data={seriesCount}
                />
                <DescriptionListItem
                  title={translate('Movies')}
                  data={movieCount}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem title={translate('Ended')} data={ended} />
                <DescriptionListItem
                  title={translate('Continuing')}
                  data={continuing}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem
                  title={translate('Monitored')}
                  data={monitored}
                />
                <DescriptionListItem
                  title={translate('Unmonitored')}
                  data={items.length - monitored}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem
                  title={translate('Episodes')}
                  data={episodes}
                />
                <DescriptionListItem
                  title={translate('Files')}
                  data={episodeFiles}
                />
                <DescriptionListItem
                  title={translate('MoviesWithFile')}
                  data={moviesWithFile}
                />
              </DescriptionList>

              <DescriptionList>
                <DescriptionListItem
                  title={translate('TotalFileSize')}
                  data={formatBytes(totalFileSize)}
                />
              </DescriptionList>
            </div>
          </div>
        );
      }}
    </ColorImpairedConsumer>
  );
}
