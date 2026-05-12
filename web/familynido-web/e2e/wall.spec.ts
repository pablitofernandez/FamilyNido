import { expect, test } from '@playwright/test';

/**
 * Publish a wall message with a unique body, verify it lands in the feed,
 * then delete it to keep the suite idempotent.
 */
test('publish + delete wall message', async ({ page }) => {
  const body = `Hola desde Playwright ${Date.now()}`;
  await page.goto('/wall');

  // Open the composer.
  await page.getByRole('button', { name: /nuevo mensaje/i }).click();

  // Type into the mention textarea (the inner textarea is what we target).
  const textarea = page.getByPlaceholder(/recuerda sacar al perro/i);
  await textarea.fill(body);

  // The preview block renders the same body once the debounced fetch lands.
  await expect(page.getByText('Vista previa')).toBeVisible();

  // Publish.
  await page.getByRole('button', { name: /publicar/i }).click();

  // The new message appears in the feed.
  const card = page.locator('article, li').filter({ hasText: body }).first();
  await expect(card).toBeVisible();

  // Tear down: the test user is the author, so the trash icon is offered.
  await card.getByRole('button', { name: /borrar/i }).click();
  // The browser confirm dialog auto-accepts — ConfirmDialog is the default.
  page.on('dialog', (dialog) => void dialog.accept());
});
