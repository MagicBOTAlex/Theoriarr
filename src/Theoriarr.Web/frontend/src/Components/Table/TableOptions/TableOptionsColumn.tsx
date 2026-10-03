import classNames from 'classnames';
import React, { useRef } from 'react';
import { DragSourceMonitor, useDrag, useDrop, XYCoord } from 'react-dnd';
import CheckInput from 'Components/Form/CheckInput';
import Icon from 'Components/Icon';
import DragType from 'Helpers/DragType';
import { icons } from 'Helpers/Props';
import { CheckInputChanged } from 'typings/inputs';
import Column, { IsModifiable } from '../Column';

interface DragItem {
  name: string;
  index: number;
}

interface TableOptionsColumnProps {
  name: string;
  label: Column['label'];
  isDraggingDown: boolean;
  isDraggingUp: boolean;
  isVisible: boolean;
  isModifiable: IsModifiable;
  index: number;
  onVisibleChange: (change: CheckInputChanged) => void;
  onColumnDragEnd: (didDrop: boolean) => void;
  onColumnDragMove: (dragIndex: number, hoverIndex: number) => void;
}

function TableOptionsColumn({
  name,
  label,
  index,
  isDraggingDown,
  isDraggingUp,
  isVisible,
  isModifiable,
  onVisibleChange,
  onColumnDragEnd,
  onColumnDragMove,
}: TableOptionsColumnProps) {
  const ref = useRef<HTMLDivElement>(null);

  const [{ isOver }, dropRef] = useDrop<DragItem, void, { isOver: boolean }>({
    accept: DragType.TableColumn,
    collect(monitor) {
      return {
        isOver: monitor.isOver(),
      };
    },
    hover(item: DragItem, monitor) {
      if (!ref.current) {
        return;
      }

      if (isModifiable === 'disabled') {
        return;
      }

      const dragIndex = item.index;
      const hoverIndex = index;

      // Don't replace items with themselves
      if (dragIndex === hoverIndex) {
        return;
      }

      // Determine rectangle on screen
      const hoverBoundingRect = ref.current?.getBoundingClientRect();

      // Get vertical middle
      const hoverMiddleY =
        (hoverBoundingRect.bottom - hoverBoundingRect.top) / 2;

      // Determine mouse position
      const clientOffset = monitor.getClientOffset();

      // Get pixels to the top
      const hoverClientY = (clientOffset as XYCoord).y - hoverBoundingRect.top;

      // When moving up, only trigger if drag position is above 50% and
      // when moving down, only trigger if drag position is below 50%.
      // If we're moving down the hoverIndex needs to be increased
      // by one so it's ordered properly. Otherwise the hoverIndex will work.

      // Dragging downwards
      if (dragIndex < hoverIndex && hoverClientY < hoverMiddleY) {
        return;
      }

      // Dragging upwards
      if (dragIndex > hoverIndex && hoverClientY > hoverMiddleY) {
        return;
      }

      onColumnDragMove(dragIndex, hoverIndex);
    },
  });

  const [{ isDragging }, dragRef, previewRef] = useDrag<
    DragItem,
    unknown,
    { isDragging: boolean }
  >({
    type: DragType.TableColumn,
    item: () => {
      return {
        name,
        index,
      };
    },
    collect: (monitor: DragSourceMonitor<unknown, unknown>) => ({
      isDragging: monitor.isDragging(),
    }),
    end: (_item: DragItem, monitor) => {
      onColumnDragEnd(monitor.didDrop());
    },
  });

  dropRef(previewRef(ref));

  const isBefore = !isDragging && isDraggingUp && isOver;
  const isAfter = !isDragging && isDraggingDown && isOver;

  return (
    <div ref={ref} className="my-1">
      {isBefore ? (
        <div className="mb-2 h-9 w-full rounded-[4px] border border-dotted border-[#aaa]" />
      ) : null}

      <div
        className={classNames(
          'flex w-full items-stretch rounded-[4px] border border-[#aaa] bg-[var(--inputBackgroundColor)]',
          isDragging && 'opacity-25'
        )}
      >
        <label className="mb-0 ml-0.5 flex grow cursor-pointer font-normal leading-9">
          <CheckInput
            containerClassName="relative mr-1 mb-[7px] ml-2"
            name={name}
            value={isVisible}
            isDisabled={isModifiable !== 'enabled'}
            onChange={onVisibleChange}
          />
          {typeof label === 'function' ? label() : label}
        </label>

        {isModifiable === 'disabled' ? null : (
          <div
            ref={dragRef}
            className="ml-auto flex w-10 shrink-0 cursor-grab items-center justify-center text-center"
          >
            <Icon className="top-0" name={icons.REORDER} />
          </div>
        )}
      </div>

      {isAfter ? (
        <div className="mt-2 h-9 w-full rounded-[4px] border border-dotted border-[#aaa]" />
      ) : null}
    </div>
  );
}

export default TableOptionsColumn;
