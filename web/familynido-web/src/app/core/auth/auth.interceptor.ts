import { HttpInterceptorFn } from '@angular/common/http';

/**
 * Ensures cookies ride along with cross-origin API requests during development
 * (Angular dev server on :4200 → .NET backend on :5080 through the proxy). In
 * production the SPA is served from the same origin as the API, so this is a
 * no-op there.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) =>
  next(req.clone({ withCredentials: true }));
