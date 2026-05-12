import { expect, test } from './fixtures';

/**
 * Multi-user happy-path: tester A creates a one-off task assigned to tester
 * B; B opens the app in a separate context, sees the task in `/tasks` →
 * Hoy, and marks it complete; A reloads and sees the row rendered with
 * line-through (B's completion is visible across contexts).
 *
 * Requires both seeded testers to exist in the same family — the
 * `E2ETestDataSeeder` (Seed:E2E:Enabled=true) takes care of that locally
 * and in CI.
 */
test('A assigns a task to B; B completes it; A sees the completion', async ({ page, pageB }) => {
  const title = `E2E multi ${Date.now()}`;
  const userBName = process.env['E2E_USER_B_NAME'] ?? 'Tester B';

  // ── A: create the task assigned to B ──────────────────────────────────
  await page.goto('/tasks');
  await page.getByRole('button', { name: /nueva tarea/i }).click();
  await page.getByPlaceholder('Fregar el baño').fill(title);
  // The "Responsable" label sits next to the select but isn't wired up
  // with `for=`, so getByLabel can't traverse to it. The select is the
  // only one in the form that has a "Sin responsable" option.
  const responsibleSelect = page.locator('select:has(option:has-text("Sin responsable"))');
  await responsibleSelect.selectOption({ label: userBName });
  await page.getByRole('button', { name: /^guardar$/i }).click();

  // Confirm A's view persisted the task.
  await page.getByRole('tab', { name: 'Todas' }).click();
  const aRow = page.locator('li').filter({ hasText: title });
  await expect(aRow).toBeVisible();

  // ── B: independent context, sees the task and completes it ────────────
  // B opens a fresh /tasks for the first time in this run; give the Hoy
  // list a bit more headroom than the default 5s so the network round-trip
  // and Angular hydration can settle. The first locator anchors on the
  // "Completar" button so the assertion only resolves once the dayList
  // has rendered; the click flips the button's aria-label to "Deshacer",
  // so the post-click assertion drops the button anchor.
  await pageB.goto('/tasks');
  await pageB.getByRole('tab', { name: 'Hoy' }).click();
  const bRowPending = pageB.locator('li')
    .filter({ hasText: title })
    .filter({ has: pageB.getByRole('button', { name: /completar/i }) });
  await expect(bRowPending).toBeVisible({ timeout: 15000 });
  await bRowPending.getByRole('button', { name: /completar/i }).click();
  const bRow = pageB.locator('li').filter({ hasText: title });
  await expect(bRow.locator('p').first()).toHaveClass(/line-through/);

  // ── A: refresh, the completion is visible from this context too ───────
  await page.reload();
  await page.getByRole('tab', { name: 'Hoy' }).click();
  const aRowAfter = page.locator('li').filter({ hasText: title });
  await expect(aRowAfter).toBeVisible();
  await expect(aRowAfter.locator('p').first()).toHaveClass(/line-through/);

  // ── Cleanup so the suite stays idempotent ─────────────────────────────
  await page.getByRole('tab', { name: 'Todas' }).click();
  await aRow.getByRole('button', { name: /archivar/i }).click();
});
