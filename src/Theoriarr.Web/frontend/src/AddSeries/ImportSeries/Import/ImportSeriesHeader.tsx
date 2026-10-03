import classNames from 'classnames';
import React from 'react';
import SeriesMonitoringOptionsPopoverContent from 'AddSeries/SeriesMonitoringOptionsPopoverContent';
import SeriesTypePopoverContent from 'AddSeries/SeriesTypePopoverContent';
import Icon from 'Components/Icon';
import VirtualTableHeader from 'Components/Table/VirtualTableHeader';
import VirtualTableHeaderCell, {
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
} from 'Components/Table/VirtualTableHeaderCell';
import VirtualTableSelectAllHeaderCell from 'Components/Table/VirtualTableSelectAllHeaderCell';
import Popover from 'Components/Tooltip/Popover';
import { icons, tooltipPositions } from 'Helpers/Props';
import { CheckInputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

const FOLDER_CLASS = classNames(
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
  'flex-[1_0_200px]'
);
const MONITOR_CLASS = classNames(
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
  'flex-[0_1_200px] min-w-[185px]'
);
const QUALITY_PROFILE_CLASS = classNames(
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
  'flex-[0_1_250px] min-w-[170px]'
);
const SERIES_TYPE_CLASS = classNames(
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
  'flex-[0_1_200px] min-w-[120px]'
);
const SEASON_FOLDER_CLASS = classNames(
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
  'flex-[0_1_150px] min-w-[120px]'
);
const SERIES_CLASS = classNames(
  VIRTUAL_TABLE_HEADER_CELL_CLASS,
  'flex-[0_1_400px] min-w-[300px]'
);
const DETAILS_ICON_CLASS = 'ml-[8px]';

interface ImportSeriesHeaderProps {
  allSelected: boolean;
  allUnselected: boolean;
  onSelectAllChange: (change: CheckInputChanged) => void;
}

function ImportSeriesHeader({
  allSelected,
  allUnselected,
  onSelectAllChange,
}: ImportSeriesHeaderProps) {
  return (
    <VirtualTableHeader>
      <VirtualTableSelectAllHeaderCell
        allSelected={allSelected}
        allUnselected={allUnselected}
        onSelectAllChange={onSelectAllChange}
      />

      <VirtualTableHeaderCell className={FOLDER_CLASS} name="folder">
        {translate('Folder')}
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell className={MONITOR_CLASS} name="monitor">
        {translate('Monitor')}

        <Popover
          anchor={<Icon className={DETAILS_ICON_CLASS} name={icons.INFO} />}
          title={translate('MonitoringOptions')}
          body={<SeriesMonitoringOptionsPopoverContent />}
          position={tooltipPositions.RIGHT}
        />
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={QUALITY_PROFILE_CLASS}
        name="qualityProfileId"
      >
        {translate('QualityProfile')}
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell className={SERIES_TYPE_CLASS} name="seriesType">
        {translate('SeriesType')}

        <Popover
          anchor={<Icon className={DETAILS_ICON_CLASS} name={icons.INFO} />}
          title={translate('SeriesType')}
          body={<SeriesTypePopoverContent />}
          position={tooltipPositions.RIGHT}
        />
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell
        className={SEASON_FOLDER_CLASS}
        name="seasonFolder"
      >
        {translate('SeasonFolder')}
      </VirtualTableHeaderCell>

      <VirtualTableHeaderCell className={SERIES_CLASS} name="series">
        {translate('Series')}
      </VirtualTableHeaderCell>
    </VirtualTableHeader>
  );
}

export default ImportSeriesHeader;
