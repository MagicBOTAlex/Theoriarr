import React, { useCallback } from 'react';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import { useSelect } from 'App/Select/SelectContext';
import Label from 'Components/Label';
import SeriesTagList from 'Components/SeriesTagList';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import { kinds } from 'Helpers/Props';
import { DownloadClientModel } from 'Settings/DownloadClients/DownloadClients/useDownloadClients';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';

interface ManageDownloadClientsModalRowProps {
  id: number;
  name: string;
  protocol: DownloadProtocol;
  enable: boolean;
  priority: number;
  removeCompletedDownloads: boolean;
  removeFailedDownloads: boolean;
  implementation: string;
  tags: number[];
  columns: Column[];
}

function ManageDownloadClientsModalRow(
  props: ManageDownloadClientsModalRowProps
) {
  const {
    id,
    name,
    protocol,
    enable,
    priority,
    removeCompletedDownloads,
    removeFailedDownloads,
    implementation,
    tags,
  } = props;

  const { toggleSelected, useIsSelected } = useSelect<DownloadClientModel>();
  const isSelected = useIsSelected(id);

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey }: SelectStateInputProps) => {
      toggleSelected({
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [toggleSelected]
  );

  return (
    <TableRow>
      <TableSelectCell
        id={id}
        isSelected={isSelected}
        onSelectedChange={handleSelectedChange}
      />

      <TableRowCell className="break-all">{name}</TableRowCell>

      <TableRowCell className="break-all">
        <ProtocolLabel protocol={protocol} />
      </TableRowCell>

      <TableRowCell className="break-all">{implementation}</TableRowCell>

      <TableRowCell className="break-all">
        <Label kind={enable ? kinds.SUCCESS : kinds.DISABLED} outline={!enable}>
          {enable ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className="break-all">{priority}</TableRowCell>

      <TableRowCell className="break-all">
        {removeCompletedDownloads ? translate('Yes') : translate('No')}
      </TableRowCell>

      <TableRowCell className="break-all">
        {removeFailedDownloads ? translate('Yes') : translate('No')}
      </TableRowCell>

      <TableRowCell className="break-all">
        <SeriesTagList tags={tags} />
      </TableRowCell>
    </TableRow>
  );
}

export default ManageDownloadClientsModalRow;
