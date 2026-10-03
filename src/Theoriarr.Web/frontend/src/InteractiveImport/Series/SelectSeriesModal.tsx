import React from 'react';
import Label from 'Components/Label';
import Modal from 'Components/Modal/Modal';
import { SelectEntityColumn } from 'InteractiveImport/SelectEntity/SelectEntity';
import SelectEntityModalContent from 'InteractiveImport/SelectEntity/SelectEntityModalContent';
import Series from 'Series/Series';
import useSeries from 'Series/useSeries';
import translate from 'Utilities/String/translate';

const columns: SelectEntityColumn<Series>[] = [
  {
    name: 'title',
    label: () => translate('Title'),
    isVisible: true,
    render: (series) => series.title,
  },
  {
    name: 'year',
    label: () => translate('Year'),
    isVisible: true,
    render: (series) => series.year,
  },
  {
    name: 'tvdbId',
    label: () => translate('TvdbId'),
    isVisible: true,
    render: (series) => <Label>{series.tvdbId}</Label>,
  },
  {
    name: 'imdbId',
    label: () => translate('ImdbId'),
    isVisible: true,
    render: (series) => (series.imdbId ? <Label>{series.imdbId}</Label> : null),
  },
];

const getSearchValues = (series: Series) => [
  series.title,
  series.tvdbId.toString(),
  series.imdbId ?? '',
];

interface SelectSeriesModalProps {
  isOpen: boolean;
  modalTitle: string;
  onSeriesSelect(series: Series): void;
  onModalClose(): void;
}

function SelectSeriesModal(props: SelectSeriesModalProps) {
  const { isOpen, modalTitle, onSeriesSelect, onModalClose } = props;
  const { data: series } = useSeries();

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <SelectEntityModalContent
        title={translate('SelectSeriesModalTitle', { modalTitle })}
        filterPlaceholder={translate('FilterSeriesPlaceholder')}
        columns={columns}
        items={series}
        getSearchValues={getSearchValues}
        onSelect={onSeriesSelect}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default SelectSeriesModal;
