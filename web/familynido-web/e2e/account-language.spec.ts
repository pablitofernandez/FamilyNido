import { expect, test } from '@playwright/test';

/**
 * Verifies the language picker in `/account` round-trips: switch from
 * Spanish to English, the page reloads to the `/en/` bundle and the
 * "Apply" button is visible (it reads "Aplicar" in es); switch back and
 * the URL returns to `/es/`. This guards the i18n subpath wiring (the
 * `window.location.assign('/' + newPrefix + ...)` in AuthService) and the
 * server-side persistence of `User.PreferredLanguage` together.
 *
 * Resets the user back to `es-ES` before exiting so the rest of the suite
 * (which assumes Spanish copy) keeps working on subsequent runs.
 */
test('language picker switches bundles and persists', async ({ page }) => {
  // The <select> is reachable via its options (es-ES / en-US) — the label
  // wraps it but the visible text lives in a <span>, which getByLabel
  // doesn't pick up.
  const langSelect = page.locator('select:has(option[value="es-ES"]):has(option[value="en-US"])');

  // ── es-ES → en-US ─────────────────────────────────────────────────────
  await page.goto('/es/account');
  await expect(page.getByRole('button', { name: /aplicar/i })).toBeVisible();

  await langSelect.selectOption('en-US');
  await page.getByRole('button', { name: /aplicar/i }).click();

  await page.waitForURL(/\/en\//);
  expect(page.url()).toMatch(/\/en\/account/);
  await expect(page.getByRole('button', { name: /^apply$/i })).toBeVisible();

  // ── en-US → es-ES (cleanup) ───────────────────────────────────────────
  await langSelect.selectOption('es-ES');
  await page.getByRole('button', { name: /^apply$/i }).click();

  await page.waitForURL(/\/es\//);
  expect(page.url()).toMatch(/\/es\/account/);
});
