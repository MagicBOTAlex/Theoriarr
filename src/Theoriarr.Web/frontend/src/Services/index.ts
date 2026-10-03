export type ServiceId = 'movies' | 'series';

export interface ServiceGlobal {
  apiKey: string;
  clientApiRoot: string;
  apiBase: string;
  signalrRoot: string;
  instanceName: string;
  theme: string;
  version: string;
}

const DEFAULT_SERVICE: ServiceId = 'series';

export function normalizeService(service?: ServiceId): ServiceId {
  return service ?? DEFAULT_SERVICE;
}

// React Query key prefix for a service+path. Series keys stay `[path]` so the
// existing series caches/invalidation keep working; movies keys are namespaced
// as `['movies', path]`.
export function getServiceQueryKey(
  service: ServiceId | undefined,
  path: string
): unknown[] {
  const id = normalizeService(service);

  return id === 'series' ? [path] : [id, path];
}

export function getService(service?: ServiceId): ServiceGlobal {
  const id = normalizeService(service);
  const globals =
    id === 'movies'
      ? window.Theoriarr.services.movies
      : window.Theoriarr.services.series;

  return {
    apiKey: globals?.apiKey ?? '',
    clientApiRoot: globals?.clientApiRoot ?? '',
    apiBase: globals?.apiBase ?? '',
    signalrRoot: globals?.signalrRoot ?? `/signalr/${id}`,
    instanceName: globals?.instanceName ?? '',
    theme: globals?.theme ?? 'auto',
    version: globals?.version ?? '',
  };
}

export function getServiceClientHeader(
  service?: ServiceId
): Record<string, string> {
  const id = normalizeService(service);

  return id === 'movies'
    ? { 'X-Radarr-Client': 'Theoriarr' }
    : { 'X-Sonarr-Client': 'Theoriarr' };
}
