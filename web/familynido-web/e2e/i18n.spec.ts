import { expect, test } from '@playwright/test';

/**
 * Smoke test for the en-US locale bundle. Hits the public login page
 * (no auth needed) on both /es/login and /en/login and verifies each
 * carries the right copy. Catches regressions where the messages.en-US.xlf
 * file falls out of sync with the source extraction (typo in trans-unit
 * id, missing target, stale source after a tag rename).
 *
 * Targets a deployed instance — same baseURL as the rest of the suite.
 * In dev (ng serve) only the source bundle is served at the root path,
 * so this spec is a no-op unless E2E_BASE_URL points at a stack served
 * by the production nginx config.
 */
test.describe('i18n bundles', () => {
  test('en/login renders English copy', async ({ page }) => {
    await page.goto('/en/login');
    // The "Sign in with email" submit button comes from the en-US.xlf
    // translation of @@auth.login.submit.
    await expect(page.getByRole('button', { name: /sign in with email/i })).toBeVisible();
  });

  test('es/login renders Spanish copy', async ({ page }) => {
    await page.goto('/es/login');
    await expect(page.getByRole('button', { name: /iniciar sesión con email/i })).toBeVisible();
  });
});
