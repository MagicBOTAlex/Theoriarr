import { getService, ServiceId } from 'Services';

// Settings live under `/api/v5` (the merged settings tree is series/V5 shaped)
// regardless of the requesting service. Movie/series REST otherwise goes through
// the per-service clientApiRoot selected by API key (see index.ts).
const SETTINGS_API_ROOT = '/api/v5';

const getQueryPath = (path: string, service?: ServiceId) => {
  if (path.startsWith('/api/')) {
    return path;
  }

  if (path.startsWith('/settings/')) {
    return SETTINGS_API_ROOT + path;
  }

  return getService(service).clientApiRoot + path;
};

export default getQueryPath;
