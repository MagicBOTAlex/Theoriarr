import React, { SyntheticEvent, useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import { icons } from 'Helpers/Props';
import { MediaItem } from '../MediaItem';

interface LibraryIndexPosterSelectProps {
  item: MediaItem;
}

function LibraryIndexPosterSelect({ item }: LibraryIndexPosterSelectProps) {
  const { toggleSelected, useIsSelected } = useSelect();
  const isSelected = useIsSelected(item.selectKey);

  const onSelectPress = useCallback(
    (event: SyntheticEvent<HTMLElement, PointerEvent>) => {
      if (event.nativeEvent.ctrlKey || event.nativeEvent.metaKey) {
        window.open(
          `${window.Theoriarr.services.series.urlBase}${item.link}`,
          '_blank'
        );
        return;
      }

      const shiftKey = event.nativeEvent.shiftKey;

      toggleSelected({
        id: item.selectKey,
        isSelected: !isSelected,
        shiftKey,
      });
    },
    [item.selectKey, item.link, isSelected, toggleSelected]
  );

  return (
    <Link
      className="group absolute top-0 left-0 z-[3] h-full w-full"
      onPress={onSelectPress}
    >
      <span className="absolute top-2 left-2 h-5 w-5 rounded-full bg-[var(--defaultColor)]">
        <Icon
          className={
            isSelected
              ? 'text-[var(--theoriarrBlue)] group-hover:text-[var(--white)]'
              : 'text-[var(--white)] group-hover:text-[var(--theoriarrBlue)]'
          }
          name={isSelected ? icons.CHECK_CIRCLE : icons.CIRCLE_OUTLINE}
          size={20}
        />
      </span>
    </Link>
  );
}

export default LibraryIndexPosterSelect;
