import { expect, test } from '@playwright/test';

/**
 * Share-link flow on the wall:
 *  - "Compartir" button copies a `/wall?messageId=<uuid>` URL to the clipboard.
 *  - Opening that URL lands on the wall with the target message highlighted
 *    and the query param cleared so a refresh doesn't re-trigger the highlight.
 *  - A bogus uuid surfaces the "not found / no access" toast and the feed
 *    still renders normally.
 *
 * The clipboard API is gated by Permissions — we grant read/write to the
 * default context per-test instead of polluting the global Playwright config.
 */

/** Create a wall message and return its id (read from the `wall-message-<id>` host id). */
async function publishMessage(page: import('@playwright/test').Page, body: string): Promise<string> {
  await page.goto('/wall');
  await page.getByRole('button', { name: /nuevo mensaje/i }).click();
  await page.getByPlaceholder(/recuerda sacar al perro/i).fill(body);
  await expect(page.getByText('Vista previa')).toBeVisible();
  await page.getByRole('button', { name: /publicar/i }).click();

  const card = page.locator(`li[id^="wall-message-"]`).filter({ hasText: body }).first();
  await expect(card).toBeVisible();
  const hostId = await card.getAttribute('id');
  expect(hostId, 'card should have a wall-message-<uuid> id').not.toBeNull();
  return hostId!.replace('wall-message-', '');
}

/** Delete a card by its body text. Used to keep the suite idempotent. */
async function deleteByBody(page: import('@playwright/test').Page, body: string): Promise<void> {
  page.once('dialog', (dialog) => void dialog.accept());
  const card = page.locator('li[id^="wall-message-"]').filter({ hasText: body }).first();
  await card.getByRole('button', { name: /borrar/i }).click();
  await expect(card).toHaveCount(0);
}

test.describe('wall share-link', () => {
  test.beforeEach(async ({ context }) => {
    await context.grantPermissions(['clipboard-read', 'clipboard-write']);
  });

  test('share button copies a /wall?messageId=… URL to the clipboard', async ({ page }) => {
    const body = `Compartible ${Date.now()}`;
    const messageId = await publishMessage(page, body);

    const card = page.locator(`#wall-message-${messageId}`);
    await card.getByRole('button', { name: /compartir enlace/i }).click();

    await expect(page.getByText(/enlace copiado/i)).toBeVisible();
    const clipboard = await page.evaluate(() => navigator.clipboard.readText());
    expect(clipboard).toContain(`/wall?messageId=${messageId}`);

    await deleteByBody(page, body);
  });

  test('deep-link lands on the message, highlights it, and cleans the URL', async ({ page }) => {
    const body = `Para deep link ${Date.now()}`;
    const messageId = await publishMessage(page, body);

    // Navigate as if we received the share link from someone else.
    await page.goto(`/wall?messageId=${messageId}`);

    const card = page.locator(`#wall-message-${messageId}`);
    await expect(card).toBeVisible();
    // The highlight class is `ring-2 ring-[color:var(--color-terra)]` — assert
    // against `ring-2`, which is the stable token.
    await expect(card).toHaveClass(/ring-2/);
    // URL got cleaned after the deep link was consumed.
    await expect(page).toHaveURL(/\/wall(?:\?(?!.*messageId).*)?$/);

    await deleteByBody(page, body);
  });

  test('deep-link to a non-existent message shows the not-found toast', async ({ page }) => {
    await page.goto('/wall?messageId=00000000-0000-0000-0000-000000000000');

    await expect(page.getByText(/ya no existe o no tienes acceso/i)).toBeVisible();
    // Feed still loads — at minimum the title is there.
    await expect(page.getByRole('heading', { name: /muro/i })).toBeVisible();
    // URL got cleaned.
    await expect(page).toHaveURL(/\/wall(?:\?(?!.*messageId).*)?$/);
  });
});
