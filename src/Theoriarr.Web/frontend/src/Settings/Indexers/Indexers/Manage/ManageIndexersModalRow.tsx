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
import { IndexerModel } from 'Settings/Indexers/useIndexers';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';

interface ManageIndexersModalRowProps {
  id: number;
  name: string;
  protocol: DownloadProtocol;
  enableRss: boolean;
  enableAutomaticSearch: boolean;
  enableInteractiveSearch: boolean;
  priority: number;
  seasonSearchMaximumSingleEpisodeAge: number;
  implementation: string;
  tags: number[];
  columns: Column[];
}

function ManageIndexersModalRow(props: ManageIndexersModalRowProps) {
  const {
    id,
    name,
    protocol,
    enableRss,
    enableAutomaticSearch,
    enableInteractiveSearch,
    priority,
    seasonSearchMaximumSingleEpisodeAge,
    implementation,
    tags,
  } = props;

  const { toggleSelected, useIsSelected } = useSelect<IndexerModel>();
  const isSelected = useIsSelected(id);

  const onSelectedChangeWrapper = useCallback(
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
        onSelectedChange={onSelectedChangeWrapper}
      />

      <TableRowCell className="break-all">{name}</TableRowCell>

      <TableRowCell className="break-all">
        <ProtocolLabel protocol={protocol} />
      </TableRowCell>

      <TableRowCell className="break-all">{implementation}</TableRowCell>

      <TableRowCell className="break-all">
        <Label
          kind={enableRss ? kinds.SUCCESS : kinds.DISABLED}
          outline={!enableRss}
        >
          {enableRss ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className="break-all">
        <Label
          kind={enableAutomaticSearch ? kinds.SUCCESS : kinds.DISABLED}
          outline={!enableAutomaticSearch}
        >
          {enableAutomaticSearch ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className="break-all">
        <Label
          kind={enableInteractiveSearch ? kinds.SUCCESS : kinds.DISABLED}
          outline={!enableInteractiveSearch}
        >
          {enableInteractiveSearch ? translate('Yes') : translate('No')}
        </Label>
      </TableRowCell>

      <TableRowCell className="break-all">{priority}</TableRowCell>

      <TableRowCell className="break-all">
        {seasonSearchMaximumSingleEpisodeAge}
      </TableRowCell>

      <TableRowCell className="break-all">
        <SeriesTagList tags={tags} />
      </TableRowCell>
    </TableRow>
  );
}

export default ManageIndexersModalRow;
