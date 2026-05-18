import { LOCALE_ID, inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Route guard: requires an authenticated, linked user. Sends users without
 * a linked member to a dedicated "not linked" screen, and anonymous users
 * to /login (which lets them choose between PocketID and local password).
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const locale = inject(LOCALE_ID);

  const status = auth.status();

  if (status === 'authenticated') {
    return true;
  }

  if (status === 'not-linked') {
    return router.parseUrl('/not-linked');
  }

  // Fresh instance, never bootstrapped → route everyone to the one-shot
  // setup wizard until they create the first admin (issue #20). Lets a
  // new self-hosted deployment surface its first screen without needing
  // OIDC configured or a manual SQL insert to seed the admin.
  if (status === 'setup-required') {
    return router.parseUrl('/setup');
  }

  // anonymous / idle / loading → kick off the chooser screen so the user
  // picks PocketID or local credentials. The OIDC flow does a server-side
  // redirect after the callback, so the returnUrl must include the locale
  // prefix (`/es/...` or `/en/...`); otherwise the browser lands on a path
  // nginx doesn't recognize and 404s.
  // `state.url` is router-relative — base href strips the prefix — so we
  // re-add it from the active LOCALE_ID.
  const prefix = locale.toLowerCase().startsWith('en') ? '/en' : '/es';
  return router.createUrlTree(['/login'], {
    queryParams: { returnUrl: prefix + state.url },
  });
};
