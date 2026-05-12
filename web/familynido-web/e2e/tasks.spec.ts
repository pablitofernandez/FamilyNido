import { expect, test } from '@playwright/test';

/**
 * Critical task flow: open /tasks, create a one-off task with a unique title,
 * mark today's occurrence as done, and verify the row gains the line-through
 * style that signals completion. Cleans up by archiving the task at the end
 * so the suite stays idempotent across runs.
 */
test('create + complete + archive a task', async ({ page }) => {
  const title = `E2E task ${Date.now()}`;
  await page.goto('/tasks');

  // Open the inline editor (the "+" button at the top of the page).
  await page.getByRole('button', { name: /nueva tarea/i }).click();

  // Fill the minimum viable task: title only — recurrence defaults to "Única",
  // start date defaults to today.
  await page.getByPlaceholder('Fregar el baño').fill(title);
  await page.getByRole('button', { name: /^guardar$/i }).click();

  // Switch to "Todas" so the new task is visible regardless of recurrence.
  await page.getByRole('tab', { name: 'Todas' }).click();
  const newRow = page.locator('li').filter({ hasText: title });
  await expect(newRow).toBeVisible();

  // Open Hoy and mark complete via its checkbox button. The first locator
  // anchors on the "Completar" button so the assertion only resolves once
  // Angular has rendered the Hoy list (without it, a stale <li> from the
  // previous "Todas" view can race the switch and confuse the selector).
  // Once we click, the same button flips its aria-label to "Deshacer", so
  // we drop the button anchor for subsequent assertions.
  await page.getByRole('tab', { name: 'Hoy' }).click();
  const todayRowPending = page.locator('li')
    .filter({ hasText: title })
    .filter({ has: page.getByRole('button', { name: /completar/i }) });
  await expect(todayRowPending).toBeVisible({ timeout: 15000 });
  await todayRowPending.getByRole('button', { name: /completar/i }).click();

  // After completion the title is rendered with line-through. Re-anchor on
  // title only — the row is now stable and only the completed instance is
  // present.
  const todayRow = page.locator('li').filter({ hasText: title });
  await expect(todayRow.locator('p').first()).toHaveClass(/line-through/);

  // Archive to keep the suite idempotent.
  await page.getByRole('tab', { name: 'Todas' }).click();
  await newRow.getByRole('button', { name: /archivar/i }).click();
});
