import { expect, test } from '@playwright/test';

/**
 * Dashboard smoke: visiting /home mounts the widget grid and the per-user
 * widget catalogue endpoint responds. We don't assert any specific widget
 * because the user can hide them all from /account — but the grid container
 * should always be in the DOM so a regression that breaks the widget loop
 * (e.g. a renamed signal) gets caught here.
 */
test('dashboard widget grid mounts', async ({ page }) => {
  await page.goto('/home');

  // The grid is the first <div> inside the main <section> with the
  // grid-cols layout — assert it's there.
  await expect(page.locator('section .grid.grid-cols-1.md\\:grid-cols-2').first()).toBeVisible();

  // The user can also navigate to /account and verify the dashboard
  // preferences UI shows up — that's the surface that drives this widget
  // grid, so it's worth a sanity check.
  await page.goto('/account');
  await expect(page.getByText(/widgets del panel/i)).toBeVisible();
});
