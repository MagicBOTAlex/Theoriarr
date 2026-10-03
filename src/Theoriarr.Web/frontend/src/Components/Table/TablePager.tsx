import classNames from 'classnames';
import React, { useCallback, useMemo, useState } from 'react';
import SelectInput, {
  SELECT_INPUT_CLASS,
  SelectInputOption,
} from 'Components/Form/SelectInput';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import LoadingIndicator, {
  LOADING_INDICATOR_CLASS,
} from 'Components/Loading/LoadingIndicator';
import { icons } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';

const PAGE_SELECT_CLASS = classNames(
  SELECT_INPUT_CLASS,
  'p-[0_2px]! h-[25px]!'
);
const PAGE_LINK_CLASS = 'p-0 w-[30px] h-[30px] leading-[30px]';
const DISABLED_PAGE_BUTTON_CLASS = 'text-[var(--disabledColor)]';

interface TablePagerProps {
  page?: number;
  totalPages?: number;
  totalRecords?: number;
  isFetching?: boolean;
  onFirstPagePress?: () => void;
  onPreviousPagePress?: () => void;
  onNextPagePress?: () => void;
  onLastPagePress?: () => void;
  onPageSelect: (page: number) => void;
}

function TablePager({
  page,
  totalPages,
  totalRecords = 0,
  isFetching,
  onPageSelect,
}: TablePagerProps) {
  const [isShowingPageSelect, setIsShowingPageSelect] = useState(false);

  const isFirstPage = page === 1;
  const isLastPage = page === totalPages;

  const pages = useMemo(() => {
    return Array.from(new Array(totalPages), (_x, i): SelectInputOption => {
      const pageNumber = i + 1;

      return {
        key: pageNumber,
        value: String(pageNumber),
      };
    });
  }, [totalPages]);

  const handleOpenPageSelectClick = useCallback(() => {
    setIsShowingPageSelect(true);
  }, []);

  const handlePageSelect = useCallback(
    ({ value }: InputChanged<number>) => {
      setIsShowingPageSelect(false);
      onPageSelect(value);
    },
    [onPageSelect]
  );

  const handlePageSelectBlur = useCallback(() => {
    setIsShowingPageSelect(false);
  }, []);

  const handleFirstPagePress = useCallback(() => {
    onPageSelect(1);
  }, [onPageSelect]);

  const onPreviousPagePress = useCallback(() => {
    if (!page) {
      return;
    }

    onPageSelect(page - 1);
  }, [onPageSelect, page]);

  const onNextPagePress = useCallback(() => {
    if (!page) {
      return;
    }

    onPageSelect(page + 1);
  }, [onPageSelect, page]);

  const onLastPagePress = useCallback(() => {
    if (!totalPages) {
      return;
    }

    onPageSelect(totalPages);
  }, [onPageSelect, totalPages]);

  if (!page) {
    return null;
  }

  return (
    <div className="flex items-center justify-between max-[992px]:flex-wrap">
      <div className="flex-[0_1_33%] max-[992px]:flex-[0_1_50%]">
        {isFetching ? (
          <LoadingIndicator
            className={classNames(
              LOADING_INDICATOR_CLASS,
              'mt-0! ml-[5px] text-left'
            )}
            size={20}
          />
        ) : null}
      </div>

      <div className="flex-[0_1_33%] flex justify-center max-[992px]:flex-[0_1_100%] max-[992px]:[order:-1]">
        <div className="flex items-center text-center">
          <Link
            className={classNames(
              PAGE_LINK_CLASS,
              isFirstPage && DISABLED_PAGE_BUTTON_CLASS
            )}
            isDisabled={isFirstPage}
            aria-label={translate('PagerGoToFirstPage')}
            onPress={handleFirstPagePress}
          >
            <Icon name={icons.PAGE_FIRST} aria-hidden={true} />
          </Link>

          <Link
            className={classNames(
              PAGE_LINK_CLASS,
              isFirstPage && DISABLED_PAGE_BUTTON_CLASS
            )}
            isDisabled={isFirstPage}
            aria-label={translate('PagerGoToPreviousPage')}
            onPress={onPreviousPagePress}
          >
            <Icon name={icons.PAGE_PREVIOUS} aria-hidden={true} />
          </Link>

          <div className="leading-[30px]">
            {isShowingPageSelect ? null : (
              <Link
                isDisabled={totalPages === 1}
                aria-label={translate('PagerGoToPage', {
                  page,
                  totalPages: totalPages ?? 0,
                })}
                onPress={handleOpenPageSelectClick}
              >
                {page} / {totalPages}
              </Link>
            )}

            {isShowingPageSelect ? (
              <SelectInput
                className={PAGE_SELECT_CLASS}
                name="pageSelect"
                value={page}
                values={pages}
                autoFocus={true}
                onChange={handlePageSelect}
                onBlur={handlePageSelectBlur}
              />
            ) : null}
          </div>

          <Link
            className={classNames(
              PAGE_LINK_CLASS,
              isLastPage && DISABLED_PAGE_BUTTON_CLASS
            )}
            isDisabled={isLastPage}
            aria-label={translate('PagerGoToNextPage')}
            onPress={onNextPagePress}
          >
            <Icon name={icons.PAGE_NEXT} aria-hidden={true} />
          </Link>

          <Link
            className={classNames(
              PAGE_LINK_CLASS,
              isLastPage && DISABLED_PAGE_BUTTON_CLASS
            )}
            isDisabled={isLastPage}
            aria-label={translate('PagerGoToLastPage')}
            onPress={onLastPagePress}
          >
            <Icon name={icons.PAGE_LAST} aria-hidden={true} />
          </Link>
        </div>
      </div>

      <div className="flex-[0_1_33%] flex justify-end max-[992px]:flex-[0_1_50%]">
        <div className="text-[var(--disabledColor)]">
          {translate('TotalRecords', { totalRecords })}
        </div>
      </div>
    </div>
  );
}

export default TablePager;
