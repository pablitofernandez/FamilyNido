import { expect, test } from '@playwright/test';

/**
 * Smoke test: with a valid session, the app shell renders, the user lands
 * on /home, and every primary tab is reachable. Catches regressions in
 * routing, the auth guard and the layout component.
 */
test.describe('app shell', () => {
  test('redirects authenticated user to /home and shows the nav', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveURL(/\/home$/);
    // Home is the dashboard — the page contains the greeting heading.
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  });

  test('every primary tab loads without 404', async ({ page }) => {
    const routes = ['/home', '/tasks', '/calendar', '/wall', '/meals', '/nido', '/account'];
    for (const route of routes) {
      await page.goto(route);
      await expect(page).toHaveURL(new RegExp(route.replace('/', '\\/') + '$'));
      // Each route renders an h1 — that's a deliberate convention across the app.
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    }
  });
});
