import React from 'react';
import HistoryEventTypeCell from 'Activity/History/HistoryEventTypeCell';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import RelativeDateCell from 'Components/Table/Cells/RelativeDateCell';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import { kinds } from 'Helpers/Props';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import { HistoryData, HistoryEventType } from 'typings/History';
import translate from 'Utilities/String/translate';

interface MovieHistoryItem {
  id: number;
  eventType: HistoryEventType;
  sourceTitle: string;
  languages?: Language[];
  quality?: QualityModel;
  date: string;
  data: HistoryData;
}

const columns: Column[] = [
  {
    name: 'eventType',
    label: '',
    isVisible: true,
  },
  {
    name: 'sourceTitle',
    label: () => translate('SourceTitle'),
    isVisible: true,
  },
  {
    name: 'quality',
    label: () => translate('Quality'),
    isVisible: true,
  },
  {
    name: 'date',
    label: () => translate('Date'),
    isVisible: true,
  },
];

export interface MovieHistoryModalContentProps {
  movieId: number;
  onModalClose: () => void;
}

const DEFAULT_HISTORY: MovieHistoryItem[] = [];

function MovieHistoryModalContent({
  movieId,
  onModalClose,
}: MovieHistoryModalContentProps) {
  const { isFetching, isFetched, error, data } = useApiQuery<
    MovieHistoryItem[]
  >({
    service: 'movies',
    path: '/history/movie',
    queryParams: { movieId },
  });

  const items = data ?? DEFAULT_HISTORY;
  const hasItems = !!items.length;

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('History')}</ModalHeader>

      <ModalBody>
        {isFetching && !isFetched ? <LoadingIndicator /> : null}

        {!isFetching && !!error ? (
          <Alert kind={kinds.DANGER}>{translate('HistoryLoadError')}</Alert>
        ) : null}

        {isFetched && !hasItems && !error ? (
          <div>{translate('NoHistory')}</div>
        ) : null}

        {isFetched && hasItems && !error ? (
          <Table columns={columns}>
            <TableBody>
              {items.map((item) => {
                return (
                  <TableRow key={item.id}>
                    <HistoryEventTypeCell
                      eventType={item.eventType}
                      data={item.data}
                    />

                    <TableRowCell>{item.sourceTitle}</TableRowCell>

                    <TableRowCell>
                      {item.quality?.quality?.name ?? ''}
                    </TableRowCell>

                    <RelativeDateCell
                      date={item.date}
                      includeSeconds={true}
                      includeTime={true}
                    />
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default MovieHistoryModalContent;
