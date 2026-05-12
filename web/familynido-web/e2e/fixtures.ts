import { test as base, type Page } from '@playwright/test';

/**
 * Playwright `test` extended with a `pageB` fixture that opens a second
 * browser context loaded with tester B's storage state. Use this in any
 * spec that needs two distinct authenticated identities — e.g. user A
 * creates a task, user B completes it, user A verifies the result.
 *
 * Single-user specs should keep importing `test` from `@playwright/test`
 * directly; only opt-in here when the second context is actually needed.
 */
export const test = base.extend<{ pageB: Page }>({
  pageB: async ({ browser }, use) => {
    const context = await browser.newContext({
      storageState: 'e2e/.auth/state-b.json',
    });
    const page = await context.newPage();
    try {
      await use(page);
    } finally {
      await context.close();
    }
  },
});

export { expect } from '@playwright/test';
