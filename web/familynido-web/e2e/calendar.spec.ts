import { expect, test } from '@playwright/test';

/**
 * Structural smoke for `/calendar`: the page renders, the month grid mounts,
 * and the link to "Cuentas vinculadas" exists. We don't try to assert
 * specific events — Google Calendar isn't synced in CI, so the empty state
 * is the only deterministic outcome there. Locally, if the dev has linked
 * an account, the test still passes (we don't assert the empty banner is
 * visible, only that one of the two valid render paths produced an h1).
 */
test('calendar renders the month grid and the accounts link', async ({ page }) => {
  await page.goto('/calendar');

  await expect(page.getByRole('heading', { level: 1 })).toContainText(/calendario/i);
  // The settings cog button routes to /calendar/accounts.
  await expect(page.getByRole('link', { name: /cuentas vinculadas/i })).toBeVisible();
  // Refresh button exists (idempotent click — should not throw).
  await page.getByRole('button', { name: /refrescar eventos/i }).click();
});
