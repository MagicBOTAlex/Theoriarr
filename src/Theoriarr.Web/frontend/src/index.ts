import type { ServiceId } from 'Services';

import './polyfills';
import 'Styles/tailwind.css';

type ServiceGlobals = Window['Theoriarr']['services']['series'];

const BASE = '';
const DEFAULT_API_ROOT = '/api/v3';
const SERIES_API_ROOT = '/api/v5';
const INITIALIZE_PATH = `${DEFAULT_API_ROOT}/initialize.json`;

// Both subsystems share the origin; the presented API key (plus the client
// header) resolves the owning subsystem server-side (design D1). The SPA is
// Sonarr's V5 frontend, so series requests use the matching `/api/v5` surface
// (the flat V3 resources differ, e.g. `/release`), while the folded-in Radarr
// movie domain only exists under `/api/v3`.
const CLIENT_API_ROOTS: Record<ServiceId, string> = {
  movies: DEFAULT_API_ROOT,
  series: SERIES_API_ROOT,
};

interface BootstrapService {
  apiRoot?: string;
  apiKey?: string;
  appName?: string;
  instanceName?: string;
  theme?: string;
  version?: string;
  signalrRoot?: string;
  isProduction?: boolean;
}

interface BootstrapPayload {
  urlBase?: string;
  isUnified?: boolean;
  release?: string;
  version?: string;
  branch?: string;
  services?: Partial<Record<ServiceId, BootstrapService>>;
}

async function fetchInitialize(): Promise<BootstrapPayload> {
  const response = await fetch(`${INITIALIZE_PATH}?t=${Date.now()}`, {
    headers: { Accept: 'application/json' },
  });

  if (!response.ok) {
    throw new Error(`Failed to load ${INITIALIZE_PATH}`);
  }

  return response.json();
}

function buildServiceGlobals(
  id: ServiceId,
  init: BootstrapService | undefined
): ServiceGlobals {
  const fallbackName = id === 'movies' ? 'Movies' : 'Series';

  return {
    ...(init as unknown as ServiceGlobals),
    urlBase: BASE,
    apiBase: BASE,
    clientApiRoot: CLIENT_API_ROOTS[id],
    apiRoot: init?.apiRoot ?? DEFAULT_API_ROOT,
    signalrRoot: init?.signalrRoot ?? `/signalr/${id}`,
    apiKey: init?.apiKey ?? '',
    instanceName: init?.instanceName ?? init?.appName ?? fallbackName,
    theme: init?.theme ?? 'auto',
    version: init?.version ?? '',
    isProduction: init?.isProduction ?? true,
  };
}

const bootstrapPayload = await fetchInitialize().catch((error: unknown) => {
  console.error('[bootstrap] failed to load initialize.json', error);

  return { isUnified: true } as BootstrapPayload;
});

window.Theoriarr = {
  urlBase: BASE,
  isUnified: bootstrapPayload.isUnified ?? true,
  services: {
    series: buildServiceGlobals('series', bootstrapPayload.services?.series),
    movies: buildServiceGlobals('movies', bootstrapPayload.services?.movies),
  },
};

/* eslint-disable no-undef, @typescript-eslint/ban-ts-comment */
// @ts-ignore 2304
__webpack_public_path__ = `${BASE}/`;
/* eslint-enable no-undef, @typescript-eslint/ban-ts-comment */

const error = console.error;

// Monkey patch console.error to filter out some warnings from React
// TODO: Remove this after the great TypeScript migration

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function logError(...parameters: any[]) {
  const filter = parameters.find((parameter) => {
    return (
      typeof parameter === 'string' &&
      (parameter.includes(
        'Support for defaultProps will be removed from function components in a future major release'
      ) ||
        parameter.includes(
          'findDOMNode is deprecated and will be removed in the next major release'
        ))
    );
  });

  if (!filter) {
    error(...parameters);
  }
}

console.error = logError;

const { bootstrap } = await import('./bootstrap');

await bootstrap();
