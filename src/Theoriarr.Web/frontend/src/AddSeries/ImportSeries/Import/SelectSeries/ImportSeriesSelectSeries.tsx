import {
  autoUpdate,
  flip,
  FloatingPortal,
  useClick,
  useDismiss,
  useFloating,
  useInteractions,
} from '@floating-ui/react';
import classNames from 'classnames';
import React, { useCallback, useEffect, useState } from 'react';
import { useLookupSeries } from 'AddSeries/AddNewSeries/useAddSeries';
import FormInputButton from 'Components/Form/FormInputButton';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import useDebounce from 'Helpers/Hooks/useDebounce';
import { icons, kinds } from 'Helpers/Props';
import useExistingSeries from 'Series/useExistingSeries';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import {
  addToLookupQueue,
  removeFromLookupQueue,
  updateImportSeriesItem,
  useImportSeriesItem,
  useIsCurrentedItemQueued,
  useIsCurrentLookupQueueItem,
} from '../importSeriesStore';
import ImportSeriesSearchResult from './ImportSeriesSearchResult';
import ImportSeriesTitle from './ImportSeriesTitle';

const SEARCH_INPUT_CLASS = classNames(INPUT_CLASS, 'rounded-none!');

const SEARCH_ICON_CONTAINER_CLASS =
  'w-[58px] border border-solid border-[var(--inputBorderColor)] border-r-0 rounded-[4px] rounded-tr-none rounded-br-none bg-[var(--searchIconContainerBackgroundColor)] text-center leading-[33px]';

const RESULTS_CLASS =
  '[scrollbar-color:var(--scrollbarBackgroundColor)_transparent] [scrollbar-width:thin] overflow-x-hidden overflow-y-scroll max-h-[165px] [&::-webkit-scrollbar]:w-[10px] [&::-webkit-scrollbar]:h-[10px] [&::-webkit-scrollbar-track]:bg-transparent [&::-webkit-scrollbar-thumb]:min-h-[100px] [&::-webkit-scrollbar-thumb]:border [&::-webkit-scrollbar-thumb]:border-solid [&::-webkit-scrollbar-thumb]:border-transparent [&::-webkit-scrollbar-thumb]:rounded-[5px] [&::-webkit-scrollbar-thumb]:bg-[var(--scrollbarBackgroundColor)] [&::-webkit-scrollbar-thumb]:[background-clip:padding-box] [&::-webkit-scrollbar-thumb:hover]:bg-[var(--scrollbarHoverBackgroundColor)]';

interface ImportSeriesSelectSeriesProps {
  id: string;
  onInputChange: (input: InputChanged) => void;
}

function ImportSeriesSelectSeries({
  id,
  onInputChange,
}: ImportSeriesSelectSeriesProps) {
  const importSeriesItem = useImportSeriesItem(id);
  const { selectedSeries, name } = importSeriesItem ?? {};
  const isExistingSeries = useExistingSeries(selectedSeries?.tvdbId);

  const [term, setTerm] = useState(name);
  const [isOpen, setIsOpen] = useState(false);
  const query = useDebounce(term, term ? 300 : 0);
  const isCurrentLookupQueueItem = useIsCurrentLookupQueueItem(id);
  const isQueued = useIsCurrentedItemQueued(id);

  const { isFetching, isFetched, error, data, refetch } = useLookupSeries(
    query,
    isCurrentLookupQueueItem
  );

  const errorMessage = getErrorMessage(error);
  const isLookingUpSeries = isFetching || isQueued;

  const handlePress = useCallback(() => {
    setIsOpen((prevIsOpen) => !prevIsOpen);
  }, []);

  const handleSearchInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTerm(value);
      addToLookupQueue(id);
    },
    [id]
  );

  const handleRefreshPress = useCallback(() => {
    refetch();
  }, [refetch]);

  const handleSeriesSelect = useCallback(
    (tvdbId: number) => {
      setIsOpen(false);

      const selectedSeries = data.find((item) => item.tvdbId === tvdbId)!;

      updateImportSeriesItem({
        id,
        selectedSeries,
      });

      if (selectedSeries.seriesType !== 'standard') {
        onInputChange({
          name: 'seriesType',
          value: selectedSeries.seriesType,
        });
      }
    },
    [id, data, onInputChange]
  );

  useEffect(() => {
    if (isFetched) {
      updateImportSeriesItem({
        id,
        hasSearched: isFetched,
        selectedSeries: data[0],
      });

      removeFromLookupQueue(id);
    }
  }, [id, isFetched, data]);

  useEffect(() => {
    setTerm(name);
  }, [name]);

  const { refs, context, floatingStyles } = useFloating({
    middleware: [
      flip({
        crossAxis: false,
        mainAxis: true,
      }),
    ],
    open: isOpen,
    placement: 'bottom',
    whileElementsMounted: autoUpdate,
    onOpenChange: setIsOpen,
  });

  const click = useClick(context);
  const dismiss = useDismiss(context);

  const { getReferenceProps, getFloatingProps } = useInteractions([
    click,
    dismiss,
  ]);

  return (
    <>
      <div ref={refs.setReference} {...getReferenceProps()}>
        <Link
          className="flex h-[35px] w-full items-center rounded-[4px] border! border-[var(--inputBorderColor)] border-[var(--inputBorderColorVisible)] bg-[var(--inputBackgroundColor)]! px-4 py-1.5 shadow-[inset_0_1px_1px_var(--inputBoxShadowColor)]"
          component="div"
          onPress={handlePress}
        >
          {isLookingUpSeries && isQueued && !isFetched ? (
            <LoadingIndicator className="inline-block" size={20} />
          ) : null}

          {isFetched && selectedSeries && isExistingSeries ? (
            <Icon
              className="mr-[8px]"
              name={icons.WARNING}
              kind={kinds.WARNING}
            />
          ) : null}

          {isFetched && selectedSeries ? (
            <ImportSeriesTitle
              title={selectedSeries.title}
              year={selectedSeries.year}
              network={selectedSeries.network}
              isExistingSeries={isExistingSeries}
            />
          ) : null}

          {isFetched && !selectedSeries ? (
            <div>
              <Icon
                className="mr-[8px]"
                name={icons.WARNING}
                kind={kinds.WARNING}
              />

              {translate('NoMatchFound')}
            </div>
          ) : null}

          {!isFetching && !!error ? (
            <div>
              <Icon
                className="mr-[8px]"
                title={errorMessage}
                name={icons.WARNING}
                kind={kinds.WARNING}
              />

              {translate('SearchFailedError')}
            </div>
          ) : null}

          <div className="flex-[1_0_auto] ml-[5px] text-right">
            <Icon name={icons.CARET_DOWN} />
          </div>
        </Link>
      </div>

      {isOpen ? (
        <FloatingPortal id="portal-root">
          <div
            ref={refs.setFloating}
            className="z-[2000] mt-[4px] w-[384px]"
            style={floatingStyles}
            {...getFloatingProps()}
          >
            {isOpen ? (
              <div className="p-[4px] border border-solid border-[var(--inputBorderColor)] border-[var(--inputBorderColorVisible)] rounded-[4px] bg-[var(--inputBackgroundColor)]">
                <div className="flex">
                  <div className={SEARCH_ICON_CONTAINER_CLASS}>
                    <Icon name={icons.SEARCH} />
                  </div>

                  <TextInput
                    className={SEARCH_INPUT_CLASS}
                    name={`${name}_textInput`}
                    value={term}
                    onChange={handleSearchInputChange}
                  />

                  <FormInputButton
                    kind={kinds.DEFAULT}
                    spinnerIcon={icons.REFRESH}
                    canSpin={true}
                    isSpinning={isFetching}
                    onPress={handleRefreshPress}
                  >
                    <Icon name={icons.REFRESH} />
                  </FormInputButton>
                </div>

                <div className={RESULTS_CLASS}>
                  {data.map((item) => {
                    return (
                      <ImportSeriesSearchResult
                        key={item.tvdbId}
                        tvdbId={item.tvdbId}
                        title={item.title}
                        year={item.year}
                        network={item.network}
                        onPress={handleSeriesSelect}
                      />
                    );
                  })}
                </div>
              </div>
            ) : null}
          </div>
        </FloatingPortal>
      ) : null}
    </>
  );
}

export default ImportSeriesSelectSeries;
