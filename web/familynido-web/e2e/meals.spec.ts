import { expect, test } from '@playwright/test';

/**
 * Lightweight smoke for `/meals`: open the planner, fill the first empty
 * "primer plato" slot for any visible day with a unique value, commit with
 * Enter, and assert the value is rendered in place. We don't try to
 * uniquely identify a specific day/course because the grid layout shifts
 * with the week — locating the first empty slot via its placeholder
 * (`¿Qué toca?`) is more robust.
 *
 * No clean-up: the persisted slot is harmless drift (one of many cells in
 * the planner) and resetting it would brittlely depend on its position.
 * If the suite needs to reset, drop the seed family in the DB.
 */
test('add a meal slot from the planner', async ({ page }) => {
  const value = `E2E ${Date.now()}`;
  await page.goto('/meals');

  // Empty slots render as buttons with placeholder copy "+ primero" or
  // "+ segundo" (firstCoursePlaceholder / secondCoursePlaceholder). The
  // editor input has its own placeholder ("¿Qué toca?") that only shows
  // after we click into edit mode.
  const firstEmpty = page.getByRole('button', { name: /^\+ (primero|segundo)$/i }).first();
  await firstEmpty.click();

  const editor = page.getByPlaceholder('¿Qué toca?');
  await expect(editor).toBeVisible();
  await editor.fill(value);
  await editor.press('Enter');

  // After commit the cell renders the value as plain text inside a button.
  await expect(page.getByRole('button', { name: value }).first()).toBeVisible();
});
