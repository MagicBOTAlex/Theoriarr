import React, { useCallback, useState } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import IconButton from 'Components/Link/IconButton';
import Link from 'Components/Link/Link';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import { icons, kinds } from 'Helpers/Props';
import { useSingleSeries } from 'Series/useSeries';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import BlocklistDetailsModal from './BlocklistDetailsModal';
import { MediaBlocklistItem } from './mediaActivity';
import { useRemoveMediaBlocklistItem } from './mediaActivityActions';
import { MediaTypeBadge } from './MediaActivityBadges';

interface MediaBlocklistRowProps {
  item: MediaBlocklistItem;
  columns: Column[];
  isSelected?: boolean;
  onSelectedChange?: (options: SelectStateInputProps<string>) => void;
}

function MediaBlocklistRow({
  item,
  columns,
  isSelected,
  onSelectedChange,
}: MediaBlocklistRowProps) {
  const series = useSingleSeries(item.seriesId);
  const { removeBlocklistItem } = useRemoveMediaBlocklistItem(
    item.type,
    item.id
  );

  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const handleDetailsPress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, []);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, []);

  const handleRemovePress = useCallback(() => {
    removeBlocklistItem();
  }, [removeBlocklistItem]);

  return (
    <TableRow>
      {onSelectedChange ? (
        <TableSelectCell
          id={item.key}
          isSelected={isSelected}
          onSelectedChange={onSelectedChange}
        />
      ) : null}

      {columns.map((column) => {
        const { name, isVisible } = column;

        if (!isVisible) {
          return null;
        }

        if (name === 'type') {
          return (
            <TableRowCell key={name}>
              <MediaTypeBadge type={item.type} />
            </TableRowCell>
          );
        }

        if (name === 'media') {
          if (series) {
            return (
              <TableRowCell key={name}>
                <Link
                  className="block max-w-[220px] truncate"
                  to={`/series/${series.titleSlug}`}
                >
                  {series.title}
                </Link>
              </TableRowCell>
            );
          }

          if (item.movieLink) {
            return (
              <TableRowCell key={name}>
                <Link
                  className="block max-w-[220px] truncate"
                  to={item.movieLink}
                >
                  {item.movieTitle}
                </Link>
              </TableRowCell>
            );
          }

          return <TableRowCell key={name} />;
        }

        if (name === 'sourceTitle') {
          return (
            <TableRowCell key={name}>
              <span className="block max-w-[360px] truncate">
                {item.sourceTitle}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'languages') {
          return (
            <TableRowCell key={name}>
              <EpisodeLanguages languages={item.languages ?? []} />
            </TableRowCell>
          );
        }

        if (name === 'quality') {
          return (
            <TableRowCell key={name}>
              {item.quality ? <EpisodeQuality quality={item.quality} /> : null}
            </TableRowCell>
          );
        }

        if (name === 'customFormats') {
          return (
            <TableRowCell key={name}>
              <EpisodeFormats formats={item.customFormats ?? []} />
            </TableRowCell>
          );
        }

        if (name === 'date') {
          return <RelativeDateCell key={name} date={item.date} />;
        }

        if (name === 'protocol') {
          return (
            <TableRowCell key={name}>
              {item.protocol ? (
                <ProtocolLabel protocol={item.protocol} />
              ) : null}
            </TableRowCell>
          );
        }

        if (name === 'indexer') {
          return (
            <TableRowCell key={name}>
              <span className="text-[12px] text-[var(--disabledColor)]">
                {item.indexer ?? '-'}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'message') {
          return (
            <TableRowCell key={name}>
              <span
                className="block max-w-[420px] truncate text-[12px] text-[var(--helpTextColor)]"
                title={item.message}
              >
                {item.message ?? '-'}
              </span>
            </TableRowCell>
          );
        }

        if (name === 'actions') {
          return (
            <TableRowCell key={name} className="whitespace-nowrap text-right">
              <IconButton
                title={translate('Details')}
                aria-label={translate('Details')}
                name={icons.INFO}
                onPress={handleDetailsPress}
              />

              <IconButton
                title={translate('RemoveFromBlocklist')}
                aria-label={translate('RemoveFromBlocklist')}
                name={icons.REMOVE}
                kind={kinds.DANGER}
                onPress={handleRemovePress}
              />
            </TableRowCell>
          );
        }

        return null;
      })}

      <BlocklistDetailsModal
        isOpen={isDetailsModalOpen}
        sourceTitle={item.sourceTitle}
        protocol={item.protocol}
        indexer={item.indexer}
        message={item.message}
        source={item.source}
        onModalClose={handleDetailsModalClose}
      />
    </TableRow>
  );
}

export default MediaBlocklistRow;
