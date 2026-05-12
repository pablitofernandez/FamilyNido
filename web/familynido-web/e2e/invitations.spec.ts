import { expect, test } from '@playwright/test';

/**
 * Onboarding wiring: as admin A, add a new family member with email +
 * "send invitation" checked, verify the invitation banner mounts with a
 * copy-link, and then clean up so the suite stays idempotent — revoke
 * the pending invitation, then permanently delete the member from its
 * detail page (a member with a pending invitation can't be deleted, so
 * the order matters).
 *
 * Email delivery is disabled in the test environment, so this exercises
 * the row-creation path without actually sending anything.
 */
test('admin invites a new member; revoke + delete cleans up after', async ({ page }) => {
  // Both the revoke action and the member-detail "Eliminar" use
  // window.confirm — accept everything that fires during this spec.
  page.on('dialog', (dialog) => dialog.accept());

  const ts = Date.now();
  const memberName = `E2E Invite ${ts}`;
  const memberEmail = `e2e-invite-${ts}@familynido.test`;

  // ── A: open /nido and submit the add-member form ───────────────────────
  await page.goto('/nido');

  // The round "+" button uses aria-label="Añadir miembro".
  await page.getByRole('button', { name: /^añadir miembro$/i }).click();

  // Labels on this form aren't wired with `for=`, so target inputs by
  // type/required attributes within the visible <form>.
  const form = page.locator('form');
  await form.locator('input[type="text"][required]').fill(memberName);
  await form.locator('input[type="email"]').fill(memberEmail);

  // The "Enviar invitación por email" checkbox is hidden until both
  // memberType=Adult and contactEmail are present. Default is unchecked
  // — we tick it so the backend mints an Invitation row alongside the
  // FamilyMember.
  await form.getByRole('checkbox', { name: /enviar invitación por email/i }).check();

  await form.getByRole('button', { name: /^guardar$/i }).click();

  // ── Assert the invitation banner mounted with a /invite/<token> link ──
  const banner = page.locator('text=Invitación creada para').locator('xpath=ancestor::div[1]');
  await expect(banner).toBeVisible();
  const linkInput = banner.locator('input[readonly]');
  const copyLink = await linkInput.inputValue();
  // The copy-link must be built from Email:AppBaseUrl (set by the
  // workflow to the SPA host) and target the Angular `/invite/:token`
  // route. The token is a URL-safe-base64 segment ≥20 chars.
  const baseUrl = process.env['E2E_BASE_URL'] ?? 'http://localhost:5173';
  expect(copyLink.startsWith(`${baseUrl}/invite/`)).toBe(true);
  expect(copyLink).toMatch(/[A-Za-z0-9._-]{20,}$/);

  // ── Cleanup #1: revoke the pending invitation ─────────────────────────
  // Pending invitations live in their own list at the bottom of /nido.
  // Scope to the <li> that mentions our email so we revoke the right one.
  const pendingRow = page.locator('li').filter({ hasText: memberEmail });
  await pendingRow.getByRole('button', { name: /^revocar$/i }).click();

  // After refresh, the pending row is gone.
  await expect(pendingRow).toHaveCount(0);

  // ── Cleanup #2: delete the member from its detail page ────────────────
  await page.locator('a').filter({ hasText: memberName }).first().click();
  await page.getByRole('button', { name: /^eliminar$/i }).click();

  // Successful delete redirects back to /nido; the member is gone.
  await page.waitForURL(/\/nido(\/|\?|$)/);
  await expect(page.locator('a').filter({ hasText: memberName })).toHaveCount(0);
});
