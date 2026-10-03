import classNames from 'classnames';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import Alert from 'Components/Alert';
import { INPUT_CLASS } from 'Components/Form/InputClassNames';
import TextInput from 'Components/Form/TextInput';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContentBody from 'Components/Page/PageContentBody';
import useDebounce from 'Helpers/Hooks/useDebounce';
import useQueryParams from 'Helpers/Hooks/useQueryParams';
import { icons, kinds } from 'Helpers/Props';
import { useHasSeries } from 'Series/useSeries';
import { InputChanged } from 'typings/inputs';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import AddNewSeriesSearchResult from './AddNewSeriesSearchResult';
import { useLookupSeries } from './useAddSeries';

const SEARCH_CONTAINER_CLASS = 'flex mb-[10px]';
const SEARCH_ICON_CONTAINER_CLASS =
  'w-[58px] h-[46px] border border-solid border-[var(--inputBorderColor)] border-r-0 rounded-[4px] rounded-tr-none rounded-br-none bg-[var(--searchIconContainerBackgroundColor)] text-center leading-[46px]';
const CLEAR_LOOKUP_BUTTON_CLASS =
  'border border-solid border-[var(--inputBorderColor)] border-l-0 rounded-tr-[4px] rounded-br-[4px] shadow-[inset_0_1px_1px_rgba(0,0,0,0.075)]';
const MESSAGE_CLASS = 'mt-[30px] text-center font-light text-[16px]';
const HELP_TEXT_CLASS = 'mb-[10px] text-[24px]';
const NO_SERIES_TEXT_CLASS = 'mt-[80px] mb-[20px]';
const NO_RESULTS_CLASS = 'mb-[10px] font-light text-[30px]';
const SEARCH_RESULTS_CLASS = 'mt-[30px]';

const SEARCH_INPUT_CLASS = classNames(
  INPUT_CLASS,
  'h-[46px] rounded-none! text-[18px]'
);

function AddNewSeries() {
  const { term: initialTerm = '' } = useQueryParams<{ term: string }>();
  const hasSeries = useHasSeries();
  const [term, setTerm] = useState(initialTerm);
  const searchInputRef = useRef<HTMLInputElement>(null);
  const [isFetching, setIsFetching] = useState(false);
  const query = useDebounce(term, term ? 300 : 0);

  const handleSearchInputChange = useCallback(
    ({ value }: InputChanged<string>) => {
      setTerm(value);
      setIsFetching(!!value.trim());
    },
    []
  );

  const handleClearSeriesLookupPress = useCallback(() => {
    setTerm('');
    setIsFetching(false);
    searchInputRef.current?.focus();
  }, []);

  const { isFetching: isFetchingApi, error, data } = useLookupSeries(query);

  useEffect(() => {
    setIsFetching(isFetchingApi);
  }, [isFetchingApi]);

  useEffect(() => {
    setTerm(initialTerm);
  }, [initialTerm]);

  return (
    <PageContentBody>
      <div className={SEARCH_CONTAINER_CLASS}>
        <div className={SEARCH_ICON_CONTAINER_CLASS}>
          <Icon name={icons.SEARCH} size={20} />
        </div>

        <TextInput
          ref={searchInputRef}
          className={SEARCH_INPUT_CLASS}
          name="seriesLookup"
          value={term}
          placeholder="eg. Breaking Bad, tvdb:####"
          autoFocus={true}
          onChange={handleSearchInputChange}
        />

        <Button
          className={CLEAR_LOOKUP_BUTTON_CLASS}
          onPress={handleClearSeriesLookupPress}
        >
          <Icon name={icons.REMOVE} size={20} />
        </Button>
      </div>

      {isFetching ? <LoadingIndicator /> : null}

      {!isFetching && !!error ? (
        <div className={MESSAGE_CLASS}>
          <div className={HELP_TEXT_CLASS}>
            {translate('AddNewSeriesError')}
          </div>

          <Alert kind={kinds.DANGER}>{getErrorMessage(error)}</Alert>
        </div>
      ) : null}

      {!isFetching && !error && !!data.length ? (
        <div className={SEARCH_RESULTS_CLASS}>
          {data.map((item) => {
            return <AddNewSeriesSearchResult key={item.tvdbId} series={item} />;
          })}
        </div>
      ) : null}

      {!isFetching && !error && !data.length && term ? (
        <div className={MESSAGE_CLASS}>
          <div className={NO_RESULTS_CLASS}>
            {translate('CouldNotFindResults', { term })}
          </div>
          <div>{translate('SearchByTvdbId')}</div>
          <div>
            <Link to="https://wiki.servarr.com/sonarr/faq#why-cant-i-add-a-new-series-when-i-know-the-tvdb-id">
              {translate('WhyCantIFindMyShow')}
            </Link>
          </div>
        </div>
      ) : null}

      {term ? null : (
        <div className={MESSAGE_CLASS}>
          <div className={HELP_TEXT_CLASS}>
            {translate('AddNewSeriesHelpText')}
          </div>
          <div>{translate('SearchByTvdbId')}</div>
        </div>
      )}

      {!term && !hasSeries ? (
        <div className={MESSAGE_CLASS}>
          <div className={NO_SERIES_TEXT_CLASS}>
            {translate('NoSeriesHaveBeenAdded')}
          </div>
          <div>
            <Button to="/add/import" kind={kinds.PRIMARY}>
              {translate('ImportExistingSeries')}
            </Button>
          </div>
        </div>
      ) : null}

      <div />
    </PageContentBody>
  );
}

export default AddNewSeries;
