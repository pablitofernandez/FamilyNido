import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import localeEn from '@angular/common/locales/en';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideRouter, withComponentInputBinding, withViewTransitions } from '@angular/router';
import { provideServiceWorker } from '@angular/service-worker';

import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';
import { routes } from './app.routes';
import { environment } from '../environments/environment';

// Register CLDR data for both bundles. With `--localize` Angular fixes
// LOCALE_ID per output bundle automatically, but the locale data tables
// (date/number formatting) still need to be loaded explicitly.
registerLocaleData(localeEs, 'es-ES');
registerLocaleData(localeEn, 'en-US');

/**
 * Application-wide providers. Enables zoneless change detection, native View
 * Transitions API navigation, HttpClient (fetch backend) with the auth
 * interceptor, and PWA service worker registration in production.
 *
 * Note: we deliberately do NOT provide LOCALE_ID. With `localize: true`
 * Angular substitutes the right value (`es-ES` or `en-US`) into each
 * compiled bundle, and a manual provider would override that and force
 * every DatePipe / Intl-style formatter back to Spanish.
 */
export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideRouter(routes, withComponentInputBinding(), withViewTransitions()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideServiceWorker('ngsw-worker.js', {
      enabled: environment.production,
      registrationStrategy: 'registerWhenStable:30000',
    }),
    provideAppInitializer(() => inject(AuthService).loadInitialSession()),
  ],
};
