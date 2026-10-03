import React, { SyntheticEvent, useCallback } from 'react';
import Icon from 'Components/Icon';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRowButton from 'Components/Table/TableRowButton';
import { icons } from 'Helpers/Props';
import { PathType } from 'Path/usePaths';

function getIconName(type: PathType) {
  switch (type) {
    case 'computer':
      return icons.COMPUTER;
    case 'drive':
      return icons.DRIVE;
    case 'file':
      return icons.FILE;
    case 'parent':
      return icons.PARENT;
    default:
      return icons.FOLDER;
  }
}

interface FileBrowserRowProps {
  type: PathType;
  name: string;
  path: string;
  actions?: React.ReactNode;
  isActionsColumnVisible?: boolean;
  onPress: (path: string) => void;
}

function FileBrowserRow(props: FileBrowserRowProps) {
  const {
    type,
    name,
    path,
    actions,
    isActionsColumnVisible = false,
    onPress,
  } = props;

  const handlePress = useCallback(() => {
    onPress(path);
  }, [path, onPress]);

  // The whole row navigates; interactive actions (the add/change menu) live in
  // the last cell and must not trigger that navigation.
  const handleActionsPress = useCallback((event: SyntheticEvent) => {
    event.stopPropagation();
  }, []);

  return (
    <TableRowButton onPress={handlePress}>
      <TableRowCell className="w-[32px]">
        <Icon name={getIconName(type)} />
      </TableRowCell>

      <TableRowCell>{name}</TableRowCell>

      {isActionsColumnVisible ? (
        <TableRowCell className="w-[1%] whitespace-nowrap text-right">
          <div onClick={handleActionsPress}>{actions}</div>
        </TableRowCell>
      ) : null}
    </TableRowButton>
  );
}

export default FileBrowserRow;
