import React, { useCallback } from 'react';
import { useSelect } from 'App/Select/SelectContext';
import SeriesTagList from 'Components/SeriesTagList';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableSelectCell from 'Components/Table/Cells/TableSelectCell';
import Column from 'Components/Table/Column';
import TableRow from 'Components/Table/TableRow';
import { ImportListModel } from 'Settings/ImportLists/ImportLists/useImportLists';
import { useQualityProfile } from 'Settings/Profiles/Quality/useQualityProfiles';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';

interface ManageImportListsModalRowProps {
  id: number;
  name: string;
  rootFolderPath: string;
  qualityProfileId: number;
  implementation: string;
  tags: number[];
  tagExisting: boolean;
  enableAutomaticAdd: boolean;
  columns: Column[];
}

function ManageImportListsModalRow(props: ManageImportListsModalRowProps) {
  const {
    id,
    name,
    rootFolderPath,
    qualityProfileId,
    implementation,
    enableAutomaticAdd,
    tags,
    tagExisting,
  } = props;

  const { toggleSelected, useIsSelected } = useSelect<ImportListModel>();
  const isSelected = useIsSelected(id);

  const qualityProfile = useQualityProfile(qualityProfileId);

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

      <TableRowCell className="break-all">{implementation}</TableRowCell>

      <TableRowCell className="break-all">
        {qualityProfile?.name ?? translate('None')}
      </TableRowCell>

      <TableRowCell className="break-all">{rootFolderPath}</TableRowCell>

      <TableRowCell className="break-all">
        {enableAutomaticAdd ? translate('Yes') : translate('No')}
      </TableRowCell>

      <TableRowCell className="break-all">
        <SeriesTagList tags={tags} />
      </TableRowCell>

      <TableRowCell className="break-all">
        {tagExisting ? translate('Yes') : translate('No')}
      </TableRowCell>
    </TableRow>
  );
}

export default ManageImportListsModalRow;
