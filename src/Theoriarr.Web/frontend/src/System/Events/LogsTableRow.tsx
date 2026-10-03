import React, { useCallback, useMemo, useState } from 'react';
import Icon from 'Components/Icon';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import TableRowButton from 'Components/Table/TableRowButton';
import { icons } from 'Helpers/Props';
import { LogEventLevel } from 'typings/LogEvent';
import LogsTableDetailsModal from './LogsTableDetailsModal';

const LEVEL_CLASSES: Record<LogEventLevel, string> = {
  trace: 'text-[#d3d3d3]',
  debug: 'text-[#808080]',
  info: 'text-[#1e90ff]',
  warn: 'text-[var(--warningColor)]',
  error: 'text-[var(--dangerColor)]',
  fatal: 'text-[var(--purple)]',
};

interface LogsTableRowProps {
  level: LogEventLevel;
  time: string;
  logger: string;
  message: string;
  exception?: string;
  columns: Column[];
}

function LogsTableRow({
  level,
  time,
  logger,
  message,
  exception,
  columns,
}: LogsTableRowProps) {
  const [isDetailsModalOpen, setIsDetailsModalOpen] = useState(false);

  const iconName = useMemo(() => {
    switch (level) {
      case 'trace':
      case 'debug':
      case 'info':
        return icons.INFO;
      case 'warn':
        return icons.DANGER;
      case 'error':
        return icons.BUG;
      case 'fatal':
        return icons.FATAL;
      default:
        return icons.UNKNOWN;
    }
  }, [level]);

  const handlePress = useCallback(() => {
    setIsDetailsModalOpen(true);
  }, []);

  const handleDetailsModalClose = useCallback(() => {
    setIsDetailsModalOpen(false);
  }, []);

  return (
    <>
      <TableRowButton onPress={handlePress}>
        {columns.map((column) => {
          const { name, isVisible } = column;

          if (!isVisible) {
            return null;
          }

          if (name === 'level') {
            return (
              <TableRowCell key={name} className="w-[20px]">
                <Icon
                  className={LEVEL_CLASSES[level]}
                  name={iconName}
                  title={level}
                />
              </TableRowCell>
            );
          }

          if (name === 'time') {
            return <RelativeDateCell key={name} date={time} />;
          }

          if (name === 'logger') {
            return <TableRowCell key={name}>{logger}</TableRowCell>;
          }

          if (name === 'message') {
            return <TableRowCell key={name}>{message}</TableRowCell>;
          }

          if (name === 'actions') {
            return <TableRowCell key={name} className="w-[45px]" />;
          }

          return null;
        })}
      </TableRowButton>

      <LogsTableDetailsModal
        isOpen={isDetailsModalOpen}
        message={message}
        exception={exception}
        onModalClose={handleDetailsModalClose}
      />
    </>
  );
}

export default LogsTableRow;
