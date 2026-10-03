import React from 'react';
import Link from 'Components/Link/Link';
import { getMediaItemPosterUrl, MediaItem } from './MediaItem';

const GRID_CLASS =
  'grid grid-cols-[repeat(auto-fill,minmax(160px,1fr))] gap-[18px] p-5';

const CARD_CLASS = 'flex flex-col text-[var(--textColor)]';

const POSTER_CONTAINER_CLASS =
  'relative aspect-[2/3] overflow-hidden rounded border border-[var(--defaultBorderColor)] bg-[var(--cardBackgroundColor)]';

const POSTER_CLASS = 'h-full w-full object-cover';

const POSTER_FALLBACK_CLASS =
  'flex h-full w-full items-center justify-center p-2 text-center text-[13px] text-[var(--disabledColor)]';

const BADGE_CLASS =
  'absolute top-[6px] left-[6px] rounded-[3px] px-[6px] py-[2px] text-[10px] font-semibold uppercase text-[var(--white)]';

interface MediaGridProps {
  items: readonly MediaItem[];
  emptyMessage?: string;
  showBadge?: boolean;
  getLink?: (item: MediaItem) => string;
  getSubtitle?: (item: MediaItem) => string;
}

const defaultGetLink = (item: MediaItem) => item.link;
const defaultGetSubtitle = (item: MediaItem) => String(item.year ?? '');

function MediaGrid({
  items,
  emptyMessage = 'Nothing to show',
  showBadge = false,
  getLink = defaultGetLink,
  getSubtitle = defaultGetSubtitle,
}: MediaGridProps) {
  if (!items.length) {
    return (
      <div className="p-5 text-[var(--disabledColor)]">{emptyMessage}</div>
    );
  }

  return (
    <div className={GRID_CLASS}>
      {items.map((item) => {
        const posterUrl = getMediaItemPosterUrl(item);
        const subtitle = getSubtitle(item);

        return (
          <Link
            key={`${item.type}-${item.id}`}
            className={CARD_CLASS}
            to={getLink(item)}
          >
            <div className={POSTER_CONTAINER_CLASS}>
              {posterUrl ? (
                <img
                  className={POSTER_CLASS}
                  src={posterUrl}
                  alt={item.title}
                  loading="lazy"
                />
              ) : (
                <div className={POSTER_FALLBACK_CLASS}>{item.title}</div>
              )}

              {showBadge ? (
                <span
                  className={`${BADGE_CLASS} ${
                    item.hasFile
                      ? 'bg-[var(--successColor)]'
                      : 'bg-[var(--dangerColor)]'
                  }`}
                >
                  {item.hasFile ? 'Downloaded' : 'Missing'}
                </span>
              ) : null}
            </div>

            <span className="mt-2 text-[13px] font-semibold text-[var(--textColor)]">
              {item.title}
            </span>
            {subtitle ? (
              <span className="mt-[2px] text-[12px] text-[var(--disabledColor)]">
                {subtitle}
              </span>
            ) : null}
          </Link>
        );
      })}
    </div>
  );
}

export default MediaGrid;
