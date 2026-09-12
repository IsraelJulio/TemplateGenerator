import { InjectionToken } from '@angular/core';

/**
 * Prefix applied to every call to the generator API.
 *
 * Empty by default: in development the dev server proxies `/api` to the API
 * (see `proxy.conf.json`), and in production both are served from the same
 * origin. Override it only when the API lives somewhere else.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => '',
});
