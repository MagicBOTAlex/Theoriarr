import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import { PropertyFilter } from 'Filters/Filter';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { getService, getServiceClientHeader, getServiceQueryKey } from 'Services';
import fetchJson from 'Utilities/Fetch/fetchJson';
import getQueryPath from 'Utilities/Fetch/getQueryPath';
import getQueryString from 'Utilities/Fetch/getQueryString';
import { QueryOptions } from './useApiQuery';

interface PagedQueryOptions<T> extends QueryOptions<PagedQueryResponse<T>> {
  page: number;
  pageSize: number;
  sortKey?: string;
  sortDirection?: SortDirection;
  filters?: PropertyFilter[];
}

export interface PagedQueryResponse<T> {
  page: number;
  pageSize: number;
  sortKey: string;
  sortDirection: string;
  totalRecords: number;
  totalPages: number;
  records: T[];
}

const DEFAULT_RECORDS: never[] = [];

const usePagedApiQuery = <T>(options: PagedQueryOptions<T>) => {
  const { requestOptions, queryKey } = useMemo(() => {
    const {
      path,
      page,
      pageSize,
      sortKey,
      sortDirection,
      filters,
      queryParams,
      queryOptions,
      service,
      ...otherOptions
    } = options;
    const serviceId = service ?? 'series';
    const queryKeyBase = getServiceQueryKey(serviceId, path);

    return {
      queryKey: [
        ...queryKeyBase,
        queryParams,
        page,
        pageSize,
        sortKey,
        sortDirection,
        filters,
      ],
      requestOptions: {
        ...otherOptions,
        path:
          getQueryPath(path, serviceId) +
          getQueryString({
            ...queryParams,
            page,
            pageSize,
            sortKey,
            sortDirection,
            filters,
          }),
        headers: {
          ...options.headers,
          'X-Api-Key': getService(serviceId).apiKey,
          ...getServiceClientHeader(serviceId),
        },
      },
    };
  }, [options]);

  const { data, ...query } = useQuery({
    ...options.queryOptions,
    queryKey,
    queryFn: async ({ signal }) => {
      const response = await fetchJson<PagedQueryResponse<T>, unknown>({
        ...requestOptions,
        signal,
      });

      return {
        ...response,
        totalPages: Math.max(
          Math.ceil(response.totalRecords / options.pageSize),
          1
        ),
      };
    },
    placeholderData: keepPreviousData,
  });

  return {
    ...query,
    queryKey,
    records: data?.records ?? DEFAULT_RECORDS,
    totalRecords: data?.totalRecords ?? 0,
    totalPages: data?.totalPages ?? 0,
  };
};

export default usePagedApiQuery;
