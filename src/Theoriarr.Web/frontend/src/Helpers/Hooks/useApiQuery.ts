import { UndefinedInitialDataOptions, useQuery } from '@tanstack/react-query';
import { useMemo } from 'react';
import { getService, getServiceClientHeader, getServiceQueryKey, ServiceId } from 'Services';
import fetchJson, {
  ApiError,
  FetchJsonOptions,
} from 'Utilities/Fetch/fetchJson';
import getQueryPath from 'Utilities/Fetch/getQueryPath';
import getQueryString, { QueryParams } from 'Utilities/Fetch/getQueryString';

export interface QueryOptions<T> extends FetchJsonOptions<unknown> {
  service?: ServiceId;
  queryParams?: QueryParams;
  queryOptions?:
    | Omit<
        UndefinedInitialDataOptions<Readonly<T>, ApiError>,
        'queryKey' | 'queryFn'
      >
    | undefined;
}

const useApiQuery = <T>(options: QueryOptions<T>) => {
  const { queryKey, requestOptions } = useMemo(() => {
    const {
      path,
      queryOptions,
      queryParams,
      service,
      ...otherOptions
    } = options;
    const serviceId = service ?? 'series';
    const queryKeyBase = getServiceQueryKey(serviceId, path);

    return {
      queryKey: queryParams
        ? [...queryKeyBase, queryParams]
        : queryKeyBase,
      requestOptions: {
        ...otherOptions,
        path: getQueryPath(path, serviceId) + getQueryString(queryParams),
        headers: {
          ...options.headers,
          'X-Api-Key': getService(serviceId).apiKey,
          ...getServiceClientHeader(serviceId),
        },
      },
    };
  }, [options]);

  return {
    queryKey,
    ...useQuery({
      ...options.queryOptions,
      queryKey,
      queryFn: async ({ signal }) =>
        fetchJson<Readonly<T>, unknown>({ ...requestOptions, signal }),
    }),
  };
};

export default useApiQuery;
