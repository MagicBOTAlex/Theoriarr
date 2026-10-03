declare module '*.css';

interface ServiceGlobals {
  apiKey: string;
  apiRoot: string;
  apiBase: string;
  clientApiRoot: string;
  signalrRoot: string;
  instanceName: string;
  theme: string;
  urlBase: string;
  version: string;
  isProduction: boolean;
  analytics?: boolean;
}

interface Window {
  Theoriarr: {
    urlBase: string;
    isUnified: boolean;
    services: {
      series: ServiceGlobals;
      movies: ServiceGlobals;
    };
  };
}
