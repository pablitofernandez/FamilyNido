import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

/**
 * Guard for the `/setup` route. Only lets visitors through when the
 * instance hasn't been bootstrapped yet (status `setup-required`).
 * Anyone else — already authenticated, plain anonymous, not-linked —
 * gets bounced to `/login`. Prevents an admin from replaying the
 * wizard once the instance is set up.
 */
export const requireSetupPendingGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.status() === 'setup-required'
    ? true
    : router.parseUrl('/login');
};

/**
 * Guard for the `/login` route. Sends the user to `/setup` when the
 * instance hasn't been bootstrapped yet — otherwise they'd see a login
 * screen for an account that doesn't exist. For initialised instances
 * it's a no-op.
 */
export const redirectIfSetupPendingGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  return auth.status() === 'setup-required'
    ? router.parseUrl('/setup')
    : true;
};
