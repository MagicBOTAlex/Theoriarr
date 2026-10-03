import anySignal from './anySignal';

export class ApiError extends Error {
  public statusCode: number;
  public statusText: string;
  public statusBody?: ApiErrorResponse;

  public constructor(
    path: string,
    statusCode: number,
    statusText: string,
    statusBody?: ApiErrorResponse
  ) {
    super(`Request Error: (${statusCode}) ${path}`);

    this.statusCode = statusCode;
    this.statusText = statusText;
    this.statusBody = statusBody;

    Object.setPrototypeOf(this, new.target.prototype);
  }
}

export interface ApiErrorResponse {
  message: string;
  details: string;
}

export interface FetchJsonOptions<TData> extends Omit<RequestInit, 'body'> {
  path: string;
  headers?: HeadersInit;
  body?: TData;
  timeout?: number;
}

// The SPA is served from the single backend origin at `/`, so there is no
// per-service URL base. Every request goes to the same origin and the
// subsystem is selected by the API key (see `Services`).
export const urlBase = '';
export const apiRoot = '/api/v3';

async function fetchJson<T, TData>({
  body,
  path,
  signal,
  timeout,
  ...options
}: FetchJsonOptions<TData>): Promise<T> {
  const abortController = new AbortController();

  let timeoutID: ReturnType<typeof setTimeout> | null = null;

  if (timeout) {
    timeoutID = setTimeout(() => {
      abortController.abort();
    }, timeout);
  }

  try {
    const response = await fetch(path, {
      ...options,
      body: body ? JSON.stringify(body) : undefined,
      headers: {
        ...options.headers,
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      signal: anySignal(abortController.signal, signal),
    });

    if (!response.ok) {
      // eslint-disable-next-line init-declarations
      let errorBody;

      try {
        errorBody = (await response.json()) as ApiErrorResponse;
      } catch {
        throw new ApiError(path, response.status, response.statusText);
      }

      throw new ApiError(path, response.status, response.statusText, errorBody);
    }

    if (response.status === 204) {
      return {} as T;
    }

    // Some endpoints (e.g. DELETE /rootFolder/{id}) return 200 with an empty
    // body. Parsing that as JSON throws and skips the mutation's onSuccess
    // handler (so caches never invalidate), so treat an empty body as `{}`.
    const text = await response.text();

    if (!text) {
      return {} as T;
    }

    return JSON.parse(text) as T;
  } finally {
    if (timeoutID) {
      clearTimeout(timeoutID);
    }
  }
}

export default fetchJson;
