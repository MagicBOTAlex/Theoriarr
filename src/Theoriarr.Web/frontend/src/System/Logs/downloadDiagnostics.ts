const SENSITIVE_KEY = /api[-_]?key|token|password|secret|authorization|cookie/i;

function sanitize(value: unknown, key = ''): unknown {
  if (key && SENSITIVE_KEY.test(key)) {
    return '********';
  }

  if (Array.isArray(value)) {
    return value.map((item) => sanitize(item));
  }

  if (value !== null && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(
        ([nestedKey, nestedValue]) => [
          nestedKey,
          sanitize(nestedValue, nestedKey),
        ]
      )
    );
  }

  return value;
}

function readStorage(storage: Storage): Record<string, string> {
  const result: Record<string, string> = {};

  for (let index = 0; index < storage.length; index += 1) {
    const key = storage.key(index);

    if (key != null) {
      result[key] = SENSITIVE_KEY.test(key)
        ? '********'
        : storage.getItem(key) ?? '';
    }
  }

  return result;
}

export function collectFrontendState() {
  const navigatorWithMemory = navigator as Navigator & {
    deviceMemory?: number;
  };

  return sanitize({
    browser: {
      userAgent: navigator.userAgent,
      platform: navigator.platform,
      language: navigator.language,
      languages: navigator.languages,
      hardwareConcurrency: navigator.hardwareConcurrency,
      deviceMemory: navigatorWithMemory.deviceMemory,
      online: navigator.onLine,
      timezone: new Intl.DateTimeFormat().resolvedOptions().timeZone,
      screen: {
        width: window.screen.width,
        height: window.screen.height,
        availWidth: window.screen.availWidth,
        availHeight: window.screen.availHeight,
        colorDepth: window.screen.colorDepth,
        pixelDepth: window.screen.pixelDepth,
      },
      viewport: {
        innerWidth: window.innerWidth,
        innerHeight: window.innerHeight,
        devicePixelRatio: window.devicePixelRatio,
      },
    },
    location: {
      href: window.location.href,
      pathname: window.location.pathname,
      hash: window.location.hash,
    },
    globals: {
      Theoriarr: window.Theoriarr,
    },
    localStorage: readStorage(window.localStorage),
    sessionStorage: readStorage(window.sessionStorage),
  });
}

export function downloadDiagnostics() {
  const state = collectFrontendState();

  return fetch(
    `${window.Theoriarr.services.series.apiBase}/api/v3/system/diagnostics`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(state),
    }
  )
    .then((response) => {
      if (!response.ok) {
        throw new Error(`Diagnostics request failed (HTTP ${response.status})`);
      }

      return response.json() as Promise<{ file: string }>;
    })
    .then(({ file }) => {
      window.location.href = `${
        window.Theoriarr.services.series.apiBase
      }/api/v3/system/diagnostics/${encodeURIComponent(file)}`;
    })
    .catch((error: unknown) => {
      // eslint-disable-next-line no-console
      console.error('[diagnostics] failed to create dump', error);
    });
}
